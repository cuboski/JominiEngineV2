using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using ProtoMessageClient;

namespace JominiAI
{
	/// <summary>
	///     Lists the actions that can be used by the agents.
	/// </summary>
	public enum MainActions
	{
		None,

		ArmyRecruit,
		ArmyMaintain,
		ArmyPickUp,
		ArmyDropOff,
		ArmyDisband,
		ArmyAppointLeader,
		ArmyPillageCurrentFief,
		ArmyAdjustCombatValues,
		ArmyAttack,

		SiegeBesiegeCurrentFief,
		SiegeStorm,
		SiegeNegotiation,
		SiegeReduction,
		SiegeEnd,

		NPCmoveDirection,
		NPCmoveToFief,
		NPCmarryPC,
		NPCmarryChild,
		NPCacceptRejectProposal,
		NPCtryForChild,
		NPChire,
		NPCfire,
		NPCbarCharacters,
		NPCunbarCharacters,
		NPCbarNationalities,
		NPCunbarNationalities,
		NPCtryEnterKeep,
		NPCexitKeep,
		NPCcamp,
		NPCaddToEntourage,
		NPCremoveFromEntourage,
		NPCappointHeir,
		NPCappointBaillif,
		NPCremoveBaillif,

		FiefAutoAdjustExpenditure,
		FiefTransferFunds,
		FiefTransferFundsToPlayer,
		FiefGrantFiefTitle,

		SpyFief,
		SpyCharacter,
		SpyArmy,

		KidnapCharacter, // Not working on server side
		KidnapRansomCaptive, // Needs KidnapCharacter to work
		KidnapReleaseCaptive, // Needs KidnapCharacter to work
		KidnapExecuteCaptive, // Needs KidnapCharacter to work
		KidnapRespondRansom, // Needs KidnapCharacter to work
	}

	public abstract class Agent : Player
	{
		/// <summary>
		///     Return the next action for the agent to execute given a pre-built semantic state.
		///     Each concrete agent implements its own decision algorithm.
		/// </summary>
		public abstract MainActions findNextAction(AgentState state, out string[] arguments);

		/// <summary>
		///     Build a semantic AgentState from the raw server-fetched GameState.
		///     Handlers and display code use this instead of working with the raw struct directly.
		/// </summary>
		public AgentState BuildState(GameState gs)
			=> AgentState.Build(gs, expenditureAdjustedThisSeasonFiefIDs, entouragedThisSeasonIDs, estimateNPCacceptableSalary, dayCostToTravelToKnownFiefs, dayCostToTravelToAdjacentFiefs, bailiffBlockedFiefIDs);

		protected List<String> nationalities = new List<String>() { "Oth", "Eng", "Fr", "Sco" };

		protected HashSet<MainActions> FailedActions = new HashSet<MainActions>(); // When failing to execute an action, it is added to this set. Is reinitialized when the agent succesfuly execute an action.

		// FailedActions is wiped on every non-camp success so a previously blocked action
		// can be retried once the world moves on. That is right for economy actions, but
		// it also means an action that fails with FIXED arguments (the war party's leader
		// appointment, say) gets re-chosen after every unrelated success and burns a slot
		// each time until the season's anti-stall cap fires. This set remembers a failed
		// (action, args) signature for the WHOLE season so the agent stops retrying that
		// exact combination while still being free to try the same action with new args.
		protected readonly HashSet<string> seasonBlockedSignatures = new HashSet<string>();

		// Actions whose refusal depends on WHO or WHAT they were aimed at rather than on the
		// action itself. A failure here blocks only that exact (action, args) for the season,
		// leaving the agent free to try the same action on the next fief or the next man.
		protected static readonly HashSet<MainActions> ArgumentScopedFailures = new HashSet<MainActions>
		{
			MainActions.NPCappointBaillif,
			MainActions.NPChire,
			MainActions.ArmyAppointLeader,
			MainActions.NPCmarryPC,
			MainActions.NPCmarryChild,
			// Already guarded per fief by expenditureAdjustedThisSeasonFiefIDs, which is filled
			// whether or not the adjustment took.
			MainActions.FiefAutoAdjustExpenditure,
		};

		protected static string ActionSignature(MainActions action, string[] args)
			=> action + "|" + string.Join(",", args ?? Array.Empty<string>());

		// True once (action, args) has failed at least once this season.
		protected bool IsSignatureBlocked(MainActions action, string[] args)
			=> seasonBlockedSignatures.Contains(ActionSignature(action, args));
		private string _lastTravelCostQueryLocation = null; // Cached location; travel costs are only re-queried when the PC moves to a new fief.
		protected HashSet<string> fiefSpiedThisSeasonIDs = new HashSet<string>();
		protected HashSet<string> charSpiedThisSeasonIDs = new HashSet<string>();
		protected HashSet<string> armySpiedThisSeasonIDs = new HashSet<string>();
		protected HashSet<string> fiefVisitedThisSeasonIDs = new HashSet<string>();
		protected int nbConsecutiveNPCmoveDirections = 0;
		protected MoveDirections lastNPCmoveDirection = MoveDirections.E; // The first value assigned to it doesn't matter
		protected string lastOfferToNPCid = null;
		protected uint lastOfferToNPCamount = 0;
		protected HashSet<string> expenditureAdjustedThisSeasonFiefIDs = new HashSet<string>();
		// Fiefs that refused a bailiff this season. The candidate list always offered the first
		// uncovered fief, so a single fief the server won't accept a bailiff for stood at the
		// head of the queue and every other fief behind it went uncovered for the whole run.
		protected HashSet<string> bailiffBlockedFiefIDs = new HashSet<string>();

		// How many seasons in a row each fief has refused a bailiff. A handful of fiefs refuse
		// every single time - the fief is listed as ours while the server disagrees about the
		// ownership - and clearing the per-season block meant those same fiefs were tried again
		// every season for the length of the run. In the 200-season dynasty run that was 2,656
		// refusals, four fifths of everything the agent did.
		//
		// After BailiffGiveUpAfter consecutive refusals a fief is left alone, and only re-tried
		// once every BailiffRetryEvery seasons, because ownership can legitimately change hands
		// and a fief that refuses today may accept later. A success clears the count outright.
		protected readonly Dictionary<string, int> bailiffRefusalCounts = new Dictionary<string, int>();
		protected const int BailiffGiveUpAfter = 3;
		protected const int BailiffRetryEvery = 25;
		protected int seasonsPlayed = 0;
		protected HashSet<string> entouragedThisSeasonIDs = new HashSet<string>(); // NPCs already added to the entourage this season; the inEntourage refresh is unreliable, so track locally to avoid re-adding the same NPC every pass.
		protected HashSet<string> proposedThisSeasonIDs = new HashSet<string>(); // Characters who have already proposed this season. Held here rather than in the GameState so it is actually cleared between seasons.

		// Foreign characters standing in a fief we have visited, keyed by charID. Their details
		// (sex, family, birth year) are what the marriage and hiring rules test, and they don't
		// change within a season, so they are fetched once and reused instead of once per action.
		protected Dictionary<string, RedProtoNPC> foreignCharCache = new Dictionary<string, RedProtoNPC>();
		// Captives per fief, likewise fetched once a season: ViewCaptives is one round-trip per
		// fief and the realm holds ~200 of them, which dominated the run time.
		protected Dictionary<string, HashSet<string>> captiveCache = new Dictionary<string, HashSet<string>>();

		// Why the server refused the action just executed, or null if it accepted. The reply
		// used to be thrown away, so a run could only report that something failed; the
		// army-leader fault went undiagnosed for exactly that reason.
		public string LastActionReason { get; protected set; }
		// True when that refusal was a step in a negotiation (a hire bid the NPC has not yet
		// accepted) rather than a decision the server judged invalid.
		public bool LastActionWasNegotiation { get; protected set; }

		protected HashSet<string> charToBarIDs = new HashSet<string>();
		protected HashSet<string> charToUnbarIDs = new HashSet<string>();
		protected HashSet<string> charToReleaseIDs = new HashSet<string>();
		protected HashSet<string> myRansomedCaptiveIDs = new HashSet<string>();

		protected Dictionary<string, double> dayCostToTravelToKnownFiefs = new Dictionary<string, double>(); // Contains the day cost to travel to any known fief
		protected Dictionary<MoveDirections, double> dayCostToTravelToAdjacentFiefs = new Dictionary<MoveDirections, double>(); // Contains the day cost to travel to any adjecent fief (the fief may not be known)

		private double estimationNbDaysUsed = 0; // For testing purpose

		protected Dictionary<MoveDirections, MoveDirections> oppositeDirections = new Dictionary<MoveDirections, MoveDirections>{
			{ MoveDirections.E, MoveDirections.W},
			{ MoveDirections.W, MoveDirections.E},
			{ MoveDirections.SE, MoveDirections.NW},
			{ MoveDirections.SW, MoveDirections.NE},
			{ MoveDirections.NE, MoveDirections.SW},
			{ MoveDirections.NW, MoveDirections.SE} };

		/// <summary>
		///		Filled with the maximum possible day cost for each action.
		/// </summary>
		protected Dictionary<MainActions, double> maxDaysToDoAction = new Dictionary<MainActions, double>{
			{MainActions.ArmyRecruit, 5},
			{MainActions.ArmyMaintain, 0},
			{MainActions.ArmyPickUp, 30},
			{MainActions.ArmyDropOff, 30},
			{MainActions.ArmyDisband, 0},
			{MainActions.ArmyAppointLeader, 0},
			{MainActions.ArmyPillageCurrentFief, 15},
			{MainActions.ArmyAdjustCombatValues, 0},
			{MainActions.ArmyAttack, 1},

			{MainActions.SiegeBesiegeCurrentFief, 0},
			{MainActions.SiegeStorm, 10},
			{MainActions.SiegeNegotiation, 10},
			{MainActions.SiegeReduction, 10},
			{MainActions.SiegeEnd, 0},

			{MainActions.NPCmoveDirection, 0}, // Calculed in function of the location as it changes a lot depending on the terrain type
			{MainActions.NPCmoveToFief, 0}, // Calculed in function of the location as it changes a lot depending on the route
			{MainActions.NPCmarryPC, 0},
			{MainActions.NPCmarryChild, 0},
			{MainActions.NPCacceptRejectProposal, 0},
			{MainActions.NPCtryForChild, 1},
			{MainActions.NPChire, 0},
			{MainActions.NPCfire, 0},
			{MainActions.NPCbarCharacters, 0},
			{MainActions.NPCunbarCharacters, 0},
			{MainActions.NPCbarNationalities, 0},
			{MainActions.NPCunbarNationalities, 0},
			{MainActions.NPCtryEnterKeep, 0},
			{MainActions.NPCexitKeep, 0},
			{MainActions.NPCcamp, 1}, // *nb days camped* => Minimum 1
			{MainActions.NPCaddToEntourage, 0},
			{MainActions.NPCremoveFromEntourage, 0},
			{MainActions.NPCappointHeir, 0},
			{MainActions.NPCappointBaillif, 0},
			{MainActions.NPCremoveBaillif, 0},

			{MainActions.FiefAutoAdjustExpenditure, 0},
			{MainActions.FiefTransferFunds, 0},
			{MainActions.FiefTransferFundsToPlayer, 0},
			{MainActions.FiefGrantFiefTitle, 0},

			{MainActions.SpyFief, 10},
			{MainActions.SpyCharacter, 10},
			{MainActions.SpyArmy, 10},

			{MainActions.KidnapCharacter, 10},
			{MainActions.KidnapRansomCaptive, 0},
			{MainActions.KidnapReleaseCaptive, 0},
			{MainActions.KidnapExecuteCaptive, 0},
			{MainActions.KidnapRespondRansom, 0},
		};

		public Agent(string username, string password) : base(username, password)
		{

		}

		/// <summary>
		///     The agent will chose and execute actions for a given number of days.
		/// </summary>
		/// <param name="withBreaks">Indicates if there are breaks after each action execution</param>
		/// <param name="maxEstimationNbDays">maximum number of days after. If the agent reaches this limit then he will stop</param>
		/// <param name="firstGS">The first GameState before the agent performs any action</param>
		/// <returns>The final gameState</returns>
		public GameState play(bool withBreaks, out GameState firstGS, int maxEstimationNbDays = 0)
		{
			firstGS = new GameState();

			List<MainActions> actionsPlayed = new List<MainActions>();
			List<string[]> actionsPlayedArguments = new List<string[]>();
			GameState gameState = new GameState();
			bool firstGSinitialized = false;
			while (true)
			{
				gameState = ObtainCurrentGSandUpdateAgentLists(gameState);
				if (!firstGSinitialized)
                {
					firstGS = gameState.deepCopy();
					firstGSinitialized = true;
				}

				Console.WriteLine("\n<----------------------------------------------->");
				RedProtoPlayerCharacter currentPC = gameState.myPC;
				RedProtoFief currenFief = gameState.getCurrentFief();
				RedProtoFief homeFief = gameState.getHomeFief();
				if(maxEstimationNbDays > 0)
					Console.WriteLine("estimationNbDaysUsed= " + estimationNbDaysUsed);
				Console.WriteLine("currentPCid= " + currentPCid);
				Console.WriteLine("currentPC.days= " + currentPC.days);
				Console.WriteLine("currenFief.fiefID= " + currenFief.fiefID);
				Console.WriteLine("currenFief.ownerID= " + currenFief.ownerID);
				Console.WriteLine("homeFief.treasury= " + homeFief.treasury);
				Console.WriteLine("Total Nb troops= " + calculMyTotalNbTroops(gameState));
				uint nbTroops = gameState.tryGetMainArmy(out RedProtoArmy mainArmy) ? Tools.CalculSumUintArray(mainArmy.troops) : 0;
				Console.WriteLine("Nb troops in mainArmy= " + nbTroops);
				Console.WriteLine("enemyPCids.Count()= " + gameState.enemyPCids.Count());
				Console.WriteLine("");

				if (withBreaks)
				{
					Console.WriteLine("---> Press a key to find and execute the next action");
					Console.ReadKey();
					Console.WriteLine("");
				}

				gameState = playOneAction(gameState, out MainActions actionChosen, out string[] actionArguments, out bool executionSuccess, out bool endOfSeason, maxEstimationNbDays);

                if (endOfSeason)
                {
					Console.WriteLine("No more actions available for this season, here is the list of executed actions: ");
					for (int i = 0; i < actionsPlayed.Count; i++)
					{
						Console.Write("\n   " + actionsPlayed[i] + "(");
						foreach (string argument in actionsPlayedArguments[i])
							Console.Write(" '" + argument + "'");
						Console.Write(" )");
					}
					Console.WriteLine("");
					return gameState;
				}
                else
                {
					Console.Write("\nThe chosen action was '" + actionChosen.ToString() + "' with the arguments: ");
					foreach (String arg in actionArguments)
						Console.Write("'" + arg + "' ");
					Console.WriteLine("");

					if (executionSuccess)
					{
						Console.WriteLine("Execution status: Success");
						actionsPlayed.Add(actionChosen);
						actionsPlayedArguments.Add(actionArguments);
					}
					else
					{
						Console.WriteLine("Execution status: Failure");
						Console.WriteLine("---> Press a key to continue");
						Console.ReadKey();
						Console.WriteLine("");
					}
				}
			}
		}

		public GameState playOneAction(GameState gameState, out MainActions actionChosen, out string[] actionArguments, out bool executionSuccess, out bool endOfSeason, double maxEstimationNbDays = 0)
        {
			AgentState state = BuildState(gameState);
			MainActions nextAction = findNextAction(state, out string[] arguments);

			actionChosen = nextAction;
			actionArguments = arguments;
			executionSuccess = true;
			endOfSeason = false;

			if (nextAction == MainActions.None || (maxEstimationNbDays > 0 && estimationNbDaysUsed >= maxEstimationNbDays)) // End of current season
				endOfSeason = true;
			else
			{
				if (ExecuteAction(nextAction, arguments, gameState, out gameState))
				{
					executionSuccess = true;
					// Camping only burns time, it doesn't fix the underlying condition that caused
					// other actions to fail, so don't clear the failure memory on a camp-only success.
					if (nextAction != MainActions.NPCcamp)
						FailedActions = new HashSet<MainActions>();
				}
                else
                {
					executionSuccess = false;
					// Sticky for the rest of the season: this exact (action, args) will
					// not be retried even after an unrelated success clears FailedActions.
					seasonBlockedSignatures.Add(ActionSignature(nextAction, arguments));
					// Some refusals are about the arguments, not the action: one fief that won't
					// take a bailiff says nothing about the other hundred, and one man refusing a
					// wage says nothing about the next. Barring the whole action for those ended
					// the season on the spot, because FailedActions only clears on a success and
					// there was nothing left that could succeed. The signature above is enough.
					if (!ArgumentScopedFailures.Contains(nextAction))
						FailedActions.Add(nextAction);
				}
			}
			return gameState;
		}

		/// <summary>
		///     Calls the ObtainCurrentGameState() function and updates the necessary class variables
		/// </summary>
		/// <param name="previousGS">Previous GameState</param>
		/// <returns>The current GameState</returns>
		public GameState ObtainCurrentGSandUpdateAgentLists(GameState previousGS = new GameState())
		{
			GameState currentGS = ObtainCurrentGameState(previousGS);

			// No living PC (dead, awaiting heir succession): skip the enrichment below,
			// which dereferences myPC/myFiefs. The caller detects myPC == null and ends the run.
			if (currentGS.myPC == null)
				return currentGS;

			// Travel costs only re-queried when the PC moves to a new fief
			RedProtoFief currentFief = currentGS.getCurrentFief();
			string currentFiefID = currentFief?.fiefID;

			bool locationChanged = currentFiefID != _lastTravelCostQueryLocation;
			if (locationChanged)
			{
				_lastTravelCostQueryLocation = currentFiefID;

				dayCostToTravelToAdjacentFiefs = new Dictionary<MoveDirections, double>();
				string[] availableTravelDirections = GetAvailableTravelDirections().MessageFields;

				if (availableTravelDirections != null)
					foreach (string availableTravelDirection in availableTravelDirections)
					{
						if (!Enum.TryParse(availableTravelDirection, out MoveDirections moveDirection))
							continue; // Skip unparseable directions rather than crashing

						var response = getTravelDayCost("", availableTravelDirection);
						double travelDayCost = 1.0;
						if (response != null && !string.IsNullOrEmpty(response.Message))
							double.TryParse(response.Message, out travelDayCost);

						dayCostToTravelToAdjacentFiefs[moveDirection] = travelDayCost;
					}

				dayCostToTravelToKnownFiefs = new Dictionary<string, double>();
				foreach (RedProtoFief knownFief in currentGS.myFiefs.Concat(currentGS.notMyFiefs))
				{
					if (knownFief == null || string.IsNullOrWhiteSpace(knownFief.fiefID)) continue;
					if (knownFief.fiefID.Equals(currentGS.myPC.location)) continue;

					var response = getTravelDayCost(knownFief.fiefID);
					double travelDayCost = 1.0;
					if (response != null && !string.IsNullOrEmpty(response.Message))
						double.TryParse(response.Message, out travelDayCost);

					dayCostToTravelToKnownFiefs[knownFief.fiefID] = travelDayCost;
				}
			}

			// Add new ennemies
			foreach (RedProtoSiegeDisplay siegeAgainstMe in currentGS.siegeOnMyFiefs)
				if (!currentGS.enemyPCids.Contains(siegeAgainstMe.besiegingPlayer))
					AddEnemy(currentGS, siegeAgainstMe.besiegingPlayer, out currentGS);
			foreach (RedProtoJournalEntry unreadJournalEntry in currentGS.unreadJournalEntries)
				if (unreadJournalEntry.type.Equals("pillage"))
					if (unreadJournalEntry.personaeIds[0].Equals(currentPCid))
						AddEnemy(currentGS, unreadJournalEntry.personaeIds[1], out currentGS);

			// Remove ransomed captives that are no longer captives
			foreach (string myRansomedCaptiveID in myRansomedCaptiveIDs)
				if(! currentGS.myCaptiveIDs.Contains(myRansomedCaptiveID))
					myRansomedCaptiveIDs.Remove(myRansomedCaptiveID);

			// charToBarIDs update
			HashSet<string> newCharToBarIDs = new HashSet<string>(charToBarIDs);
			foreach (string charToBarID in charToBarIDs)
				if (!CanBarCharFromOneOfMyFiefs(currentGS, charToBarID, out RedProtoFief redProtoFief))
					newCharToBarIDs.Remove(charToBarID); // Remove
			charToBarIDs = newCharToBarIDs;
			foreach (string enemyPCid in currentGS.enemyPCids)
				if (CanBarCharFromOneOfMyFiefs(currentGS, enemyPCid, out RedProtoFief redProtoFief))
					charToBarIDs.Add(enemyPCid); // Add

			// charToUnbarIDs and charToReleaseIDs update
			HashSet<string> newcharToUnbarIDs = new HashSet<string>(charToUnbarIDs);
			foreach (string charToUnbarID in charToUnbarIDs)
            {
				bool canUnbarChar = false;
				foreach (RedProtoFief myFief in currentGS.myFiefs)
					if (myFief.barredCharactersId.Contains(charToUnbarID))
					{
						canUnbarChar = true;
						break;
					}
				if (!canUnbarChar)
					newcharToUnbarIDs.Remove(charToUnbarID);  // Remove
			}
			charToUnbarIDs = newcharToUnbarIDs;
			foreach (string allyPCid in currentGS.allyPCids)
			{
				if(currentGS.myCaptiveIDs.Contains(allyPCid))
					charToReleaseIDs.Add(allyPCid);
				foreach (RedProtoFief myFief in currentGS.myFiefs)
					if (myFief.barredCharactersId.Contains(allyPCid))
						charToUnbarIDs.Add(allyPCid); // Add
			}
			foreach (RedProtoCharacter myNPCorPC in currentGS.myFamilyNPCs.Cast<RedProtoCharacter>().Concat(currentGS.myEmployeeNPCs).Concat(new RedProtoCharacter[] { currentGS.myPC }))
				foreach (RedProtoFief myFief in currentGS.myFiefs)
					if(myFief.barredCharactersId != null)
						if (myFief.barredCharactersId.Contains(myNPCorPC.charID))
						{
							charToUnbarIDs.Add(myNPCorPC.charID); // Add
							break;
						}

			fiefVisitedThisSeasonIDs.Add(currentFief.fiefID);

			return currentGS;
		}

		/// <summary>
		///     Fills a GameState with all the relevant information currently available by requesting them to the server.
		///     If previousGS isn't null, we take into account the information previously collected.
		/// </summary>
		/// <param name="previousGS">The previous GameState from which we will keep certain information</param>
		/// <returns>The GameState that corresponds to the current situation</returns>
		private GameState ObtainCurrentGameState(GameState previousGS)
		{
			GameState currentGS = ObtainCurrentGameState();
			RedProtoPlayerCharacter currentPC = currentGS.myPC;

			if (previousGS.notMyFiefs != null) // Check the previous GameState isn't empty
			{
				currentGS.allyPCids = previousGS.allyPCids;
				currentGS.enemyPCids = previousGS.enemyPCids;

				if (previousGS.currentYear > currentGS.currentYear)
					currentGS.currentYear = previousGS.currentYear;

				currentGS.spiedFiefs = previousGS.spiedFiefs;
				currentGS.spiedCharacters = previousGS.spiedCharacters;
				currentGS.spiedArmies = previousGS.spiedArmies;

				List<RedProtoFief> newNotMyFiefs = new List<RedProtoFief>(currentGS.notMyFiefs);
				foreach (RedProtoFief previousNotMyFief in previousGS.notMyFiefs)
				{
					bool isAlreadyInList = false;
					foreach (RedProtoFief currentNotMyFief in currentGS.notMyFiefs)
						if (previousNotMyFief.fiefID.Equals(currentNotMyFief.fiefID))
						{
							isAlreadyInList = true;
							break;
						}
					if (!isAlreadyInList)
						newNotMyFiefs.Add(RedDeepCopier.DeepCopyRedProtoFief(previousNotMyFief));
				}
				currentGS.notMyFiefs = newNotMyFiefs;
				newNotMyFiefs = new List<RedProtoFief>(currentGS.notMyFiefs);
				foreach (RedProtoFief currentNotMyFief in currentGS.notMyFiefs)
					foreach (RedProtoFief currentMyFief in currentGS.myFiefs)
						if (currentNotMyFief.fiefID.Equals(currentMyFief.fiefID)) // If I took one of the ennemy fief, then update the list
						{
							newNotMyFiefs.Remove(currentNotMyFief);
							break;
						}
				currentGS.notMyFiefs = newNotMyFiefs;

				foreach (RedProtoNPC previousNotMyNpc in previousGS.notMyNPCs)
				{
					bool isAlreadyInList = false;
					foreach (RedProtoNPC currentNotMyNpc in currentGS.notMyNPCs)
						if (previousNotMyNpc.charID.Equals(currentNotMyNpc.charID))
						{
							isAlreadyInList = true;
							break;
						}
					bool isNowMyNPC = false;
					if (!isAlreadyInList)
                    {
						foreach (RedProtoNPC currentMyNpc in currentGS.myEmployeeNPCs.Concat(currentGS.myFamilyNPCs))
							if (currentMyNpc.charID.Equals(previousNotMyNpc.charID))
							{
								isNowMyNPC = true;
								break;
							}
						if(!isNowMyNPC)
						currentGS.notMyNPCs.Add(RedDeepCopier.DeepCopyRedProtoNPC(previousNotMyNpc));
					}

				}

				foreach (RedProtoPlayerCharacter previousNotMyPc in previousGS.notMyPCs)
				{
					bool isAlreadyInList = false;
					foreach (RedProtoPlayerCharacter currentNotMyPcList in currentGS.notMyPCs)
						if (previousNotMyPc.charID.Equals(currentNotMyPcList.charID))
						{
							isAlreadyInList = true;
							break;
						}
					if (!isAlreadyInList)
						currentGS.notMyPCs.Add(RedDeepCopier.DeepCopyRedProtoPlayerCharacter(previousNotMyPc));
				}

				foreach (RedProtoArmy previousNotMyArmy in previousGS.notMyArmies)
				{
					bool isAlreadyInList = false;
					foreach (RedProtoArmy currentNotMyArmy in currentGS.notMyArmies)
						if (previousNotMyArmy.armyID.Equals(currentNotMyArmy.armyID))
						{
							isAlreadyInList = true;
							break;
						}
					if (!isAlreadyInList)
						currentGS.notMyArmies.Add(RedDeepCopier.DeepCopyRedProtoArmy(previousNotMyArmy));
				}

				if(previousGS.hasReceivedRansomForMyPC && currentGS.myPC != null && ! string.IsNullOrWhiteSpace(currentGS.myPC.captor)) // Check if still captive
                {
					currentGS.hasReceivedRansomForMyPC = true;
					currentGS.ransomJentryID = previousGS.ransomJentryID;
				}

			}
			return currentGS;
		}

		/// <summary>
		///     Resets all per-season tracking state. Call between seasons so the
		///     agent does not skip actions it already tried in the previous season.
		/// </summary>
		public virtual void ResetSeasonState()
		{
			FailedActions.Clear();
			seasonBlockedSignatures.Clear();
			expenditureAdjustedThisSeasonFiefIDs.Clear();

			// Carry forward the fiefs that have refused often enough to be written off, except
			// on the periodic retry season, when they all get another chance.
			seasonsPlayed++;
			bailiffBlockedFiefIDs.Clear();
			if (seasonsPlayed % BailiffRetryEvery == 0)
				bailiffRefusalCounts.Clear();
			else
				foreach (var kv in bailiffRefusalCounts)
					if (kv.Value >= BailiffGiveUpAfter)
						bailiffBlockedFiefIDs.Add(kv.Key);

			entouragedThisSeasonIDs.Clear();
			fiefSpiedThisSeasonIDs.Clear();
			charSpiedThisSeasonIDs.Clear();
			armySpiedThisSeasonIDs.Clear();
			fiefVisitedThisSeasonIDs.Clear();
			proposedThisSeasonIDs.Clear();
			// Marriages, deaths and births land at season boundaries, so the cached view of
			// who is available (and who is held captive where) has to be re-read now.
			foreignCharCache.Clear();
			captiveCache.Clear();
			nbConsecutiveNPCmoveDirections = 0;
			lastOfferToNPCid = null;
			lastOfferToNPCamount = 0;
			estimationNbDaysUsed = 0;
		}

		/// <summary>Record that a character of mine has proposed this season.</summary>
		protected void MarkProposed(GameState gameState, string charID)
		{
			proposedThisSeasonIDs.Add(charID);
			if (gameState.myCharsWhoProposedThisSeason != null)
				gameState.myCharsWhoProposedThisSeason[charID] = true;
		}

		/// <summary>
		///     Drop a foreign character from the cache so their details are re-read. Needed when
		///     we do something that changes them mid-season, e.g. betroth them: otherwise the
		///     stale copy still looks unattached and the next family member proposes to her too.
		/// </summary>
		protected void InvalidateForeignChar(string charID)
		{
			if (!string.IsNullOrWhiteSpace(charID))
				foreignCharCache.Remove(charID);
		}

		// Last season rollover this agent knows about. The server hands it back on every
		// SeasonUpdate; it is how a client waiting on the rest of the table notices that the
		// season it asked for has actually happened.
		private uint seasonEpoch = 0;
		// How long to wait for the other players before giving up, and how often to ask.
		private const int SeasonWaitTimeoutMs = 300_000;
		private const int SeasonPollMs = 500;

		/// <summary>
		///     Advance the server-side season: applies every fief's income/expenses and
		///     refreshes each character's day allowance. The server does NOT auto-advance
		///     seasons (UseAutoUpdate=false), so the client must request this explicitly
		///     once the PC has spent its days. Returns true if the server acknowledged it.
		/// </summary>
		public bool AdvanceSeason()
		{
			DateTime start = DateTime.Now;
			bool announcedWait = false;

			while (true)
			{
				ProtoMessage reply = SeasonUpdate(seasonEpoch);
				if (reply == null)
					return false;

				uint.TryParse(reply.Message, out uint replyEpoch);

				if (reply.ResponseType == DisplayMessages.Success)
				{
					seasonEpoch = replyEpoch;
					return true;
				}

				// Anything other than the "still waiting" answer is a real failure.
				if (reply.ResponseType != DisplayMessages.None)
					return false;

				if (!announcedWait)
				{
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine("  [SEASON] Finished; waiting for the other players to finish theirs.");
					Console.ResetColor();
					announcedWait = true;
				}

				if ((DateTime.Now - start).TotalMilliseconds > SeasonWaitTimeoutMs)
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"  [SEASON] Gave up after {SeasonWaitTimeoutMs / 1000}s waiting for the other players.");
					Console.ResetColor();
					return false;
				}

				Thread.Sleep(SeasonPollMs);
			}
		}

		/// <summary>
		///     Fill a GameState with all the relevant information currently available by requesting them to the server.
		/// </summary>
		/// <returns>The GameState that corresponds to the current situation</returns>
		protected GameState ObtainCurrentGameState()
		{
			GameState currentGS = new GameState();

			currentGS.myCurrentPCid = currentPCid;
			currentGS.enemyPCids = new HashSet<String>();
			currentGS.allyPCids = new HashSet<String>();


			ProtoGenericArray<ProtoPlayer> playersArray = GetPlayers() as ProtoGenericArray<ProtoPlayer>;
			currentGS.ExistingPlayers = new List<RedProtoPlayer>();
			if (playersArray?.fields != null)
				foreach (ProtoPlayer protoPlayer in playersArray.fields.ToList())
					currentGS.ExistingPlayers.Add(new RedProtoPlayer(protoPlayer));

			currentGS.hasReceivedRansomForMyPC = false; // TO CONTINUE

			// While the player's PC is dead and no heir has succeeded yet, the server
			// answers every request with a plain error ProtoMessage. Bail out with an
			// empty state whose myPC is left null; callers use that to end the run
			// gracefully instead of crashing on a failed cast further down.
			if (!(ViewCharacter(currentPCid) is ProtoPlayerCharacter myPcProto))
			{
				currentGS.myFamilyNPCs = new List<RedProtoNPC>();
				currentGS.myEmployeeNPCs = new List<RedProtoNPC>();
				currentGS.myFiefs = new List<RedProtoFief>();
				currentGS.myArmies = new List<RedProtoArmy>();
				currentGS.mySieges = new List<RedProtoSiegeDisplay>();
				currentGS.siegeOnMyFiefs = new List<RedProtoSiegeDisplay>();
				currentGS.myCaptiveIDs = new HashSet<string>();
				currentGS.notMyFiefs = new List<RedProtoFief>();
				currentGS.notMyNPCs = new List<RedProtoNPC>();
				currentGS.notMyPCs = new List<RedProtoPlayerCharacter>();
				currentGS.notMyArmies = new List<RedProtoArmy>();
				currentGS.notMyGarrisons = new List<RedProtoArmy>();
				currentGS.spiedFiefs = new Dictionary<string, RedProtoFief>();
				currentGS.spiedCharacters = new Dictionary<string, RedProtoCharacter>();
				currentGS.spiedArmies = new Dictionary<string, RedProtoArmy>();
				currentGS.unreadJournalEntries = new List<RedProtoJournalEntry>();
				return currentGS;
			}
			currentGS.myPC = new RedProtoPlayerCharacter(myPcProto);
			currentGS.myFamilyNPCs = new List<RedProtoNPC>();
			ProtoCharacterOverview[] myCharArray = ((ProtoGenericArray<ProtoCharacterOverview>)GetNPCList("Family")).fields;
			if (myCharArray != null && myCharArray.Length > 0)
				foreach (ProtoCharacterOverview myChar in myCharArray)
                {
					//if (int.Parse(myChar.charID.Substring(5)) > 300) // If is an NPC
					//currentGS.myFamilyNPCs.Add(new RedProtoNPC((ProtoNPC)ViewCharacter(myChar.charID)));
					ProtoCharacter protoCharacter = (ProtoCharacter)ViewCharacter(myChar.charID);
					if(protoCharacter is ProtoNPC)
						currentGS.myFamilyNPCs.Add(new RedProtoNPC((ProtoNPC)protoCharacter));
				}
			currentGS.myEmployeeNPCs = new List<RedProtoNPC>();
			ProtoCharacterOverview[] myCharArrayBis = ((ProtoGenericArray<ProtoCharacterOverview>)GetNPCList("Employee")).fields;
			if (myCharArrayBis != null && myCharArrayBis.Length > 0)
				foreach (ProtoCharacterOverview myChar in myCharArrayBis)
				{
					currentGS.myEmployeeNPCs.Add(new RedProtoNPC((ProtoNPC)ViewCharacter(myChar.charID))); // Should be an NPC
				}
			currentGS.myFiefs = new List<RedProtoFief>();
			ProtoGenericArray<ProtoFief> myFiefsArray = (ProtoGenericArray<ProtoFief>)ViewMyFiefs();
			if (myFiefsArray.fields != null)
				foreach(ProtoFief protoFief in myFiefsArray.fields)
					currentGS.myFiefs.Add(new RedProtoFief(protoFief));
			currentGS.myArmies = new List<RedProtoArmy>();
			ProtoArmyOverview[] armyStatus = ((ProtoGenericArray<ProtoArmyOverview>)ListArmies()).fields;
			if (armyStatus != null)
				foreach (ProtoArmyOverview protoArmyOverview in armyStatus)
					if(!protoArmyOverview.armyID.Contains("Garrison"))
						currentGS.myArmies.Add(new RedProtoArmy((ProtoArmy)ViewArmy(protoArmyOverview.armyID)));
			currentGS.mySieges = new List<RedProtoSiegeDisplay>();
			currentGS.siegeOnMyFiefs = new List<RedProtoSiegeDisplay>();
			ProtoSiegeOverview[] protoSiegeOverviewArray = ((ProtoGenericArray<ProtoSiegeOverview>)SiegeList()).fields;
			if (protoSiegeOverviewArray != null)
				foreach (ProtoSiegeOverview protoSiegeOverview in protoSiegeOverviewArray)
					if(protoSiegeOverview.defendingPlayer.Equals(currentPCid))
						currentGS.siegeOnMyFiefs.Add(new RedProtoSiegeDisplay((ProtoSiegeDisplay)ViewSiege(protoSiegeOverview.siegeID)));
					else
						currentGS.mySieges.Add(new RedProtoSiegeDisplay((ProtoSiegeDisplay)ViewSiege(protoSiegeOverview.siegeID)));
			// Captives change only when someone is taken or released, both of which are rare and
			// both of which invalidate the affected fief below. Reading every fief on every
			// action meant ~200 round-trips per decision and was most of the wall-clock time.
			currentGS.myCaptiveIDs = new HashSet<string>();
			foreach (RedProtoFief myFief in currentGS.myFiefs)
			{
				if (!captiveCache.TryGetValue(myFief.fiefID, out HashSet<string> captivesHere))
				{
					captivesHere = new HashSet<string>();
					ProtoMessage reply = ViewCaptives(myFief.fiefID);
					if (reply is ProtoGenericArray<ProtoCharacterOverview> protoGenericArray
						&& protoGenericArray.fields != null)
						foreach (ProtoCharacterOverview captive in protoGenericArray.fields)
							captivesHere.Add(captive.charID);
					captiveCache[myFief.fiefID] = captivesHere;
				}
				currentGS.myCaptiveIDs.UnionWith(captivesHere);
			}

			RedProtoPlayerCharacter currentPC = currentGS.myPC; // getCurrentCharacter()
			RedProtoFief currentFief = new RedProtoFief((ProtoFief)ViewFief(currentPC.location));

			currentGS.notMyFiefs = new List<RedProtoFief>();
			if(! currentFief.ownerID.Equals(currentPC.charID))
				currentGS.notMyFiefs.Add(currentFief);

			currentGS.notMyNPCs = new List<RedProtoNPC>();
			currentGS.notMyPCs = new List<RedProtoPlayerCharacter>();
			// These are the people the PC can marry, marry a child to, or hire, and every one of
			// those rules reads sex, family and birth year. Storing charID-only stubs left those
			// fields at their defaults, so the filters silently matched nobody: a widowed lord
			// never remarried, never fathered another child, and the line died out heirless.
			// The details are fetched once per season and cached (see foreignCharCache).
			// Own PC and own NPCs were already fetched above.
			foreach (string characterInFiefId in currentFief.characterInFiefIDs)
			{
				if (characterInFiefId.Equals(currentPC.charID))
					continue;
				bool isMyNPC = false;
				foreach (RedProtoNPC owned in currentGS.myFamilyNPCs.Concat(currentGS.myEmployeeNPCs))
					if (owned.charID.Equals(characterInFiefId)) { isMyNPC = true; break; }
				if (isMyNPC)
					continue;

				if (!foreignCharCache.TryGetValue(characterInFiefId, out RedProtoNPC foreignNPC))
				{
					foreignNPC = ViewCharacter(characterInFiefId) is ProtoNPC foreignProto
						? new RedProtoNPC(foreignProto)
						: null;   // a foreign PC, or a character the server won't show us
					foreignCharCache[characterInFiefId] = foreignNPC;
				}
				if (foreignNPC != null)
					currentGS.notMyNPCs.Add(foreignNPC);
			}
			currentGS.spiedFiefs = new Dictionary<string, RedProtoFief>();
			currentGS.spiedCharacters = new Dictionary<string, RedProtoCharacter>();
			currentGS.spiedArmies = new Dictionary<string, RedProtoArmy>();

			currentGS.notMyArmies = new List<RedProtoArmy>();
			currentGS.notMyGarrisons = new List<RedProtoArmy>();
			if (currentFief.armyIDs != null)
				foreach (string armyInFiefId in currentFief.armyIDs)
                {
					ProtoArmy armyInFief = (ProtoArmy)ViewArmy(armyInFiefId);
                    if(! armyInFief.ownerID.Equals(currentPC.charID))
                    {
						if (armyInFiefId.Contains("Garrison"))
							currentGS.notMyGarrisons.Add(new RedProtoArmy(armyInFief));
						else
							currentGS.notMyArmies.Add(new RedProtoArmy(armyInFief));
					}
				}
			currentGS.currentYear = 1194; // The actual version of the server starts in 1194
			currentGS.receivedProposalFromThisChar = null;
			currentGS.unreadJournalEntries = new List<RedProtoJournalEntry>();
			ProtoJournalEntry[] UnreadJournalEntriesArray = ViewJournalEntries("unread").fields;
			if (UnreadJournalEntriesArray != null)
				foreach (ProtoJournalEntry protoJournalEntry in UnreadJournalEntriesArray)
                {
					currentGS.unreadJournalEntries.Add(new RedProtoJournalEntry(protoJournalEntry));
					if (protoJournalEntry.year > currentGS.currentYear)
						currentGS.currentYear = (int)protoJournalEntry.year;
					// A proposal is ours to answer only if we are the bride's head of family.
					// This used to be read off fixed positions in the personae array and treated
					// "the groom is not me" as "somebody proposed to me" - so a lord who married
					// a son off saw his OWN proposal as one addressed to him, tried to accept it,
					// and was refused because the reply may only come from the bride's side. The
					// entry stays unread until it is properly answered, so he tried again every
					// pass: in the two-agent run that was 185 actions, every one of them refused.
					//
					// Roles are read by name rather than by index because the server drops any
					// persona whose character it cannot resolve, which shifts every position.
					if (protoJournalEntry.type.Equals("proposalMade") && !protoJournalEntry.replied)
					{
						ProtoCharacterOverview brideHead = null, bride = null;
						foreach (ProtoCharacterOverview persona in protoJournalEntry.personae)
						{
							if (persona?.role == null) continue;
							if (persona.role.Equals("headOfFamilyBride")) brideHead = persona;
							else if (persona.role.Equals("bride")) bride = persona;
						}
						if (brideHead != null && bride != null
							&& brideHead.charID.Equals(currentPC.charID))
						{
							currentGS.receivedProposalFromThisChar = new RedProtoCharacterOverview(bride);
							currentGS.receivedProposalJEntryID = protoJournalEntry.jEntryID;
						}
					}
				}
			currentGS.armyDetachmentsForMe = new List<RedProtoDetachment>();
			foreach (RedProtoArmy myArmy in currentGS.myArmies)
			{
				ProtoDetachment[] detachmentsFromArmy = ((ProtoGenericArray<ProtoDetachment>)ListDetachments(myArmy.armyID)).fields;
				if (detachmentsFromArmy != null)
					foreach (ProtoDetachment detachmentFromArmy in detachmentsFromArmy)
						if (detachmentFromArmy.leftFor.Equals(currentPCid))
							currentGS.armyDetachmentsForMe.Add(new RedProtoDetachment(detachmentFromArmy));
			}
			// Seeded from the agent-side set, which ResetSeasonState clears; the dictionary used
			// to be carried over from the previous GameState instead, so a character who
			// proposed once was treated as having proposed for the rest of the run.
			currentGS.myCharsWhoProposedThisSeason = new Dictionary<string, bool>();
			currentGS.myCharsWhoProposedThisSeason[currentPCid] = proposedThisSeasonIDs.Contains(currentPCid);
			foreach (RedProtoNPC myFamilyNPC in currentGS.myFamilyNPCs)
				currentGS.myCharsWhoProposedThisSeason[myFamilyNPC.charID] = proposedThisSeasonIDs.Contains(myFamilyNPC.charID);

			return currentGS;
		}

		/// <summary>Search for all the possible actions to execute from a certain Gamestate</summary>
		/// <param name="gameState">GameState from which to check what are the available actions to perform</param>
		/// <returns>The list of all actions the agent can perform from this GameSate</returns>
		protected List<MainActions> getPossibleActions(GameState gameState)
		{
			RedProtoPlayerCharacter currentPC = gameState.myPC;
			RedProtoFief currentFief = gameState.getCurrentFief();
			RedProtoFief homeFief = gameState.getHomeFief();

			HashSet<MainActions> possibleActions = new HashSet<MainActions>();

			if (currentPC.isAlive && currentPC.days > 0)
			{
				if (! string.IsNullOrWhiteSpace(currentPC.captor))
				{
					if (gameState.hasReceivedRansomForMyPC)
						possibleActions.Add(MainActions.KidnapRespondRansom);
				}
				else
				{
					if (dayCostToTravelToAdjacentFiefs.Count > 0 && dayCostToTravelToAdjacentFiefs.Values.Min() <= currentPC.days)
						possibleActions.Add(MainActions.NPCmoveDirection);
					if (dayCostToTravelToKnownFiefs.Count > 0 && dayCostToTravelToKnownFiefs.Values.Min() <= currentPC.days)
						possibleActions.Add(MainActions.NPCmoveToFief);
					possibleActions.Add(MainActions.NPCcamp);
					foreach (RedProtoNPC myFamilyNPC in gameState.myFamilyNPCs)
						if (!String.IsNullOrWhiteSpace(myFamilyNPC.familyID) && myFamilyNPC.isMale)
							if (myFamilyNPC.familyID.Equals(currentPC.familyID))
							{
								possibleActions.Add(MainActions.NPCappointHeir);
								break;
							}
					if (gameState.myArmies.Count > 0)
					{
						possibleActions.Add(MainActions.ArmyDisband);
						foreach (RedProtoArmy myArmy in gameState.myArmies)
                            if (!string.IsNullOrWhiteSpace(myArmy.leaderID))
                            {
								possibleActions.Add(MainActions.ArmyDropOff);
								break;
							}
						foreach (RedProtoCharacter myPCorEmployeeNPC in gameState.myEmployeeNPCs.Concat(new RedProtoCharacter[]{ gameState.myPC }))
                        {
							if (possibleActions.Contains(MainActions.ArmyAppointLeader))
								break;
							if (myPCorEmployeeNPC.isMale)
								foreach (RedProtoArmy myArmy in gameState.myArmies)
									if(currentFief.armyIDs != null)
										if (currentFief.armyIDs.Contains(myArmy.armyID) && myPCorEmployeeNPC.location.Equals(currentFief.fiefID))
											// Compare charIDs. Matching on the leader's display name meant a man
											// who already led the army still looked like a valid candidate, so
											// the appointment was offered and then refused by the server.
											if (string.IsNullOrWhiteSpace(myArmy.leaderID))
											{
												possibleActions.Add(MainActions.ArmyAppointLeader);
												break;
											}
											else
											{
												if (!myArmy.leaderID.Equals(myPCorEmployeeNPC.charID))
												{
													possibleActions.Add(MainActions.ArmyAppointLeader);
													break;
												}
											}
						}
						if (!currentFief.ownerID.Equals(currentPC.charID) && currentFief.siege == null)
						{
							foreach (RedProtoArmy myArmy in gameState.myArmies)
								if (gameState.tryGetArmyFiefID(myArmy.armyID, out string armyFiefID))
									if(armyFiefID.Equals(currentFief.fiefID))
									{
										possibleActions.Add(MainActions.SiegeBesiegeCurrentFief);
										if (!currentFief.isPillaged)
											possibleActions.Add(MainActions.ArmyPillageCurrentFief);
										break;
									}
						}
						if (currentFief.armyIDs != null)
							foreach (string armyInFiefID in currentFief.armyIDs)
                            {
								bool isMine = false;
								foreach (RedProtoArmy myArmy in gameState.myArmies)
									if (armyInFiefID.Equals(myArmy.armyID))
									{
										isMine = true;
										break;
									}
                                if (!isMine)
                                {
									possibleActions.Add(MainActions.ArmyAttack);
									break;
								}
							}
						foreach (RedProtoDetachment protoDetachment in gameState.armyDetachmentsForMe)
							if (protoDetachment.leftFor.Equals(currentPC.charID))
							{
								possibleActions.Add(MainActions.ArmyPickUp);
								break;
							}
						foreach (RedProtoArmy myArmy in gameState.myArmies)
							if (!myArmy.isMaintained && homeFief.treasury >= myArmy.maintCost)
                            {
								possibleActions.Add(MainActions.ArmyMaintain);
								break;
							}
						int indivTroopCost = 2000;
						if (currentFief.ancestralOwnerId != null)
							if (currentFief.ancestralOwnerId.Equals(currentPC.charID))
								indivTroopCost = 500;
						if(gameState.tryGetMainArmyFiefID(out string mainArmyFiefID))
							if (mainArmyFiefID.Equals(currentFief.fiefID) && currentFief.ownerID.Equals(currentPC.charID) && !currentFief.hasRecruited && homeFief.treasury >= indivTroopCost)
							{
								possibleActions.Add(MainActions.ArmyRecruit);
							}
						possibleActions.Add(MainActions.ArmyAdjustCombatValues);
					}
					foreach(RedProtoSiegeDisplay mySiege in gameState.mySieges)
					if (mySiege.besiegedFiefID.Equals(currentFief.fiefID))
					{
						possibleActions.Add(MainActions.SiegeStorm);
						possibleActions.Add(MainActions.SiegeReduction);
						possibleActions.Add(MainActions.SiegeNegotiation);
						possibleActions.Add(MainActions.SiegeEnd);
					}
					if (gameState.myFiefs.Count > 0)
					{
						if (!possibleActions.Contains(MainActions.NPCbarCharacters))
						{
							bool canBeBarred = false;
							foreach (RedProtoCharacter notMyChar in gameState.notMyNPCs.Cast<RedProtoCharacter>().Concat(gameState.notMyPCs.Cast<RedProtoCharacter>()))
								if (canBeBarred = CanBarCharFromOneOfMyFiefs(gameState, notMyChar.charID, out RedProtoFief redProtoFief))
									break;
							if (!canBeBarred)
                            {
								foreach (RedProtoFief notMyFief in gameState.notMyFiefs)
									if (CanBarCharFromOneOfMyFiefs(gameState, notMyFief.ownerID, out RedProtoFief redProtoFief))
										break;
								foreach (RedProtoArmy notMyArmy in gameState.notMyArmies)
									if (CanBarCharFromOneOfMyFiefs(gameState, notMyArmy.ownerName, out RedProtoFief redProtoFief))
										break;
							}
							if (canBeBarred)
								possibleActions.Add(MainActions.NPCbarCharacters);
						}
						foreach (RedProtoFief myFief in gameState.myFiefs)
						{
							if(!possibleActions.Contains(MainActions.FiefAutoAdjustExpenditure) && myFief.treasury < 0)
								possibleActions.Add(MainActions.FiefAutoAdjustExpenditure);
							if (!possibleActions.Contains(MainActions.FiefTransferFunds) && gameState.notMyFiefs.Count > 0 || gameState.myFiefs.Count >= 2)
								possibleActions.Add(MainActions.FiefTransferFunds);
							if (!possibleActions.Contains(MainActions.NPCunbarCharacters) && myFief.barredCharactersId != null)
								if (myFief.barredCharactersId.Length > 0)
									possibleActions.Add(MainActions.NPCunbarCharacters);
							if (!possibleActions.Contains(MainActions.NPCbarNationalities) || !possibleActions.Contains(MainActions.NPCbarNationalities))
							{
								bool hasBaredAllNationalities = true;
								foreach (String nationality in nationalities)
									if (myFief.barredNationalities != null)
										if (myFief.barredNationalities.Contains(nationality))
										{
											if (!possibleActions.Contains(MainActions.NPCbarNationalities))
												possibleActions.Add(MainActions.NPCunbarNationalities);
										}
										else
											hasBaredAllNationalities = false;
								if (!possibleActions.Contains(MainActions.NPCbarNationalities) && !hasBaredAllNationalities)
									possibleActions.Add(MainActions.NPCbarNationalities);
							}
						}
						if (gameState.ExistingPlayers.Count > 2 && homeFief.treasury > 0)
							possibleActions.Add(MainActions.FiefTransferFundsToPlayer);
						if (gameState.notMyFiefs.Count > 0)
                        {
							foreach (RedProtoFief myFief in gameState.myFiefs)
								if (myFief.ancestralOwnerId == null || currentPC.titles.Contains("King"))
									possibleActions.Add(MainActions.FiefGrantFiefTitle);
						}
					}
					if (gameState.myEmployeeNPCs.Count > 0)
                    {
						possibleActions.Add(MainActions.NPCfire);
						foreach (RedProtoNPC myNpc in gameState.myFamilyNPCs.Concat(gameState.myEmployeeNPCs))
							if (myNpc.inEntourage)
							{
								possibleActions.Add(MainActions.NPCremoveFromEntourage);
								break;
							}
						foreach (RedProtoNPC myNPC in gameState.myFamilyNPCs.Concat(gameState.myEmployeeNPCs))
							if (!myNPC.inEntourage && !string.IsNullOrWhiteSpace(myNPC.location))
								if (myNPC.location.Equals(currentFief.fiefID))
								{
									possibleActions.Add(MainActions.NPCaddToEntourage);
									break;
								}
						foreach (RedProtoNPC myEmployeeNpc in gameState.myEmployeeNPCs)
							if (myEmployeeNpc.isMale && string.IsNullOrWhiteSpace(myEmployeeNpc.captor) && gameState.currentYear - myEmployeeNpc.birthYear >= 14)
							{
								possibleActions.Add(MainActions.NPCappointBaillif);
								break;
							}
						foreach (RedProtoFief myFief in gameState.myFiefs)
							if (! string.IsNullOrWhiteSpace(myFief.bailiffID))
							{
								possibleActions.Add(MainActions.NPCremoveBaillif);
								break;
							}
					}
					if (! string.IsNullOrWhiteSpace(currentPC.spouse))
					{
						if (! gameState.tryGetCharacter(currentPC.spouse, out RedProtoCharacter spouse))
							throw new Exception("Shouldn't happen");
						if (! spouse.isPregnant && spouse.location.Equals(currentFief.fiefID) && (string.IsNullOrWhiteSpace(currentFief.siege)
						|| currentPC.inKeep == spouse.inKeep) && spouse.days > 1)
							possibleActions.Add(MainActions.NPCtryForChild);
					}
					if (gameState.receivedProposalFromThisChar != null)
						possibleActions.Add(MainActions.NPCacceptRejectProposal);
					foreach (RedProtoNPC notMyNPC in gameState.notMyNPCs)
						if (!notMyNPC.isMale && string.IsNullOrWhiteSpace(notMyNPC.spouse) && string.IsNullOrWhiteSpace(notMyNPC.fiancee) && string.IsNullOrWhiteSpace(notMyNPC.captor)
							&& !string.IsNullOrWhiteSpace(notMyNPC.familyID) && !notMyNPC.familyID.Equals(currentPC.familyID) // stub NPCs have null familyID, skip them
							&& gameState.currentYear - notMyNPC.birthYear >= 14 && notMyNPC.birthYear > 0) // Check age and that the NPC is fully populated
						{
							if (!gameState.myCharsWhoProposedThisSeason.TryGetValue(currentPCid, out bool hasCurrentPCproposed))
								throw new Exception("Couldn't find '" + currentPCid + "', it shouldn't happen");
							if (string.IsNullOrWhiteSpace(currentPC.spouse) && string.IsNullOrWhiteSpace(currentPC.fiancee) && !hasCurrentPCproposed)
								possibleActions.Add(MainActions.NPCmarryPC);
							foreach(RedProtoNPC myFamilyNPC in gameState.myFamilyNPCs)
                            {
								if (!gameState.myCharsWhoProposedThisSeason.TryGetValue(myFamilyNPC.charID, out bool hasMyChildProposed))
									throw new Exception("Couldn't find '" + myFamilyNPC.charID + "', it shouldn't happen");
								if (myFamilyNPC.isMale && string.IsNullOrWhiteSpace(myFamilyNPC.spouse) && string.IsNullOrWhiteSpace(myFamilyNPC.fiancee) && !hasMyChildProposed)
								{
									possibleActions.Add(MainActions.NPCmarryChild);
									break;
								}
							}
							break;
						}
					if (gameState.receivedProposalFromThisChar != null && string.IsNullOrWhiteSpace(currentPC.spouse) && string.IsNullOrWhiteSpace(currentPC.fiancee))
						possibleActions.Add(MainActions.NPCacceptRejectProposal);
					if (currentPC.inKeep)
						possibleActions.Add(MainActions.NPCexitKeep);
					else
					{
						bool canEnterKeep = true;
						bool hasArmyInFief = false;
						foreach (RedProtoArmy myArmy in gameState.myArmies)
							if (gameState.tryGetArmyFiefID(myArmy.armyID, out string armyFiefID))
                                if (armyFiefID.Equals(currentFief.fiefID))
                                {
									hasArmyInFief = true;
									break;
								}
						if (currentFief.barredNationalities != null)
							if (currentFief.barredNationalities.Contains(currentPC.nationality))
								canEnterKeep = false;
						if (currentFief.barredCharactersId != null)
							if (currentFief.barredCharactersId.Contains(currentPC.charID))
								canEnterKeep = false;
						if (hasArmyInFief && !currentFief.ownerID.Equals(currentPC.charID))
							canEnterKeep = false;
						if (canEnterKeep)
							possibleActions.Add(MainActions.NPCtryEnterKeep);
					}
					foreach (string charInFiefId in currentFief.characterInFiefIDs)
					{
						if(! gameState.tryGetCharacter(charInFiefId, out RedProtoCharacter charInFief))
							throw new Exception("Couldn't find the character '" + charInFiefId + "', it shouldn't happen");
						bool isMine = false;
						if (!string.IsNullOrWhiteSpace(charInFief.familyID))
							if (charInFief.familyID.Equals(currentPC.familyID))
								isMine = true;
						if (charInFief is RedProtoNPC)
							if (!string.IsNullOrWhiteSpace((charInFief as RedProtoNPC).employerId))
							{
								if ((charInFief as RedProtoNPC).employerId.Equals(currentPC.charID))
									isMine = true;
							}
							else
							{
								if (!possibleActions.Contains(MainActions.NPChire) && charInFief.isMale && gameState.currentYear - charInFief.birthYear >= 14 && string.IsNullOrWhiteSpace(charInFief.familyID)) // Check age (only if I have an estimation of the current year))
										possibleActions.Add(MainActions.NPChire);
							}
						if (!isMine)
							if (!possibleActions.Contains(MainActions.SpyCharacter))
								possibleActions.Add(MainActions.SpyCharacter);
						if (possibleActions.Contains(MainActions.SpyCharacter) && possibleActions.Contains(MainActions.NPChire))
							break;
					}
					if (currentFief.armyIDs != null)
						foreach (string armyInFiefId in currentFief.armyIDs)
						{
                            if (!armyInFiefId.Contains("Garrison"))
                            {
								if (!gameState.tryGetArmy(armyInFiefId, out RedProtoArmy armyInFief))
									throw new Exception("Couldn't find the army'" + armyInFiefId + "', it shouldn't happen");
								bool isMine = false;
								foreach (RedProtoArmy myArmy in gameState.myArmies)
									if (myArmy.armyID.Equals(armyInFiefId))
									{
										isMine = true;
										break;
									}
								if (!isMine && !armySpiedThisSeasonIDs.Contains(armyInFiefId))
								{
									possibleActions.Add(MainActions.SpyArmy);
									break;
								}
							}
						}
					if (!currentFief.ownerID.Equals(currentPC.charID) && !fiefSpiedThisSeasonIDs.Contains(currentFief.fiefID))
						possibleActions.Add(MainActions.SpyFief);
					foreach (string characterIdInFief in currentFief.characterInFiefIDs)
					{
						if (gameState.tryGetNotMyPC(characterIdInFief, out RedProtoPlayerCharacter notMyCharacter))
							if (string.IsNullOrWhiteSpace(notMyCharacter.captor))
							{
								possibleActions.Add(MainActions.KidnapCharacter);
								break;
							}
					}
					if (gameState.myCaptiveIDs.Count > 0)
					{
						possibleActions.Add(MainActions.KidnapReleaseCaptive);
						possibleActions.Add(MainActions.KidnapExecuteCaptive);
						if (myRansomedCaptiveIDs.Count < gameState.myCaptiveIDs.Count)
							possibleActions.Add(MainActions.KidnapRansomCaptive);
					}
				}
			}
			List<MainActions> newPossibleActions = new List<MainActions>(possibleActions);
			foreach (MainActions possibleAction in possibleActions)
            {
				if (!maxDaysToDoAction.TryGetValue(possibleAction, out double maxCostToDoAction))
					throw new Exception("The key '" + possibleAction.ToString() + "' isn't present in this dictionnary, it needs to be added!");
				if (maxCostToDoAction > currentPC.days || FailedActions.Contains(possibleAction))
					newPossibleActions.Remove(possibleAction);
			}
			return newPossibleActions;
		}

		/// <summary>
		///     Execute an action and check if it was correctly executed.
		/// </summary>
		/// <param name="actionToExecute">action to execute</param>
		/// <param name="arguments">arguments to execute the action with</param>
		/// <param name="gameState">The current GameState so we can modify it if it's needed</param>
		/// <param name="gameStateWithModifs">Modified GameState</param>
		/// <returns></returns>
		protected bool ExecuteAction(MainActions actionToExecute, String[] arguments, GameState gameState, out GameState gameStateWithModifs)
		{
			RedProtoFief currentFief = gameState.getCurrentFief();
			ProtoMessage protoMessage;
			DisplayMessages respType;
			bool executionSuccess = false;
			LastActionReason = null;
			LastActionWasNegotiation = false;
			switch (actionToExecute)
			{
				case MainActions.ArmyRecruit:
					executionSuccess = RecruitTroops(arguments[0], int.Parse(arguments[1])).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.ArmyMaintain:
					executionSuccess = MaintainArmy(arguments[0]).ResponseType == DisplayMessages.ArmyMaintainConfirm;
					break;
				case MainActions.ArmyPickUp:
					executionSuccess = PickUpTroops(arguments[0], arguments.Skip(1).ToArray()).ResponseType == DisplayMessages.ArmyMaintainConfirm;
					if(executionSuccess)
						foreach(RedProtoDetachment detachmentInCurrentFief in gameState.armyDetachmentsForMe)
                            if (detachmentInCurrentFief.id.Equals(arguments[0]))
                            {
								AddAlly(gameState, detachmentInCurrentFief.leftBy, out gameState); // The one that gave me the army become an ally
								break;
                            }
					break;
				case MainActions.ArmyDropOff:
					if (arguments.Length != 9)
						throw new Exception("arguments hasn't the correct length");
					uint[] troopsToDropOff = new uint[7];
					for (int i = 0; i < 7; i++)
						troopsToDropOff[i] = uint.Parse(arguments[i+1]);
;					executionSuccess = DropOffTroops(arguments[0], troopsToDropOff, arguments[arguments.Count() - 1]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.ArmyDisband:
					executionSuccess = DisbandArmy(arguments[0]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.ArmyAppointLeader:
					// A successful appointment returns a ProtoArmy (ResponseType defaults to
					// None); anything else is a rejection carrying the reason. Surface that
					// reason so a failing war run says WHY the leader was refused (wrong
					// location, not of age, already leading, unauthorised) instead of a bare
					// "fail" - this is the single fault that keeps the war path dark.
					ProtoMessage leaderReply = AppointLeader(arguments[0], arguments[1]);
					executionSuccess = leaderReply.ResponseType == DisplayMessages.None;
					if (!executionSuccess)
					{
						LastActionReason = leaderReply.ResponseType.ToString();
						Console.ForegroundColor = ConsoleColor.DarkYellow;
						Console.WriteLine($"  [WAR] AppointLeader({arguments[0]}, {arguments[1]}) refused: {leaderReply.ResponseType}");
						Console.ResetColor();
					}
					break;
				case MainActions.ArmyPillageCurrentFief:
					executionSuccess = PillageFief(arguments[0]).ResponseType == DisplayMessages.Success;
					if (executionSuccess)
						AddEnemy(gameState, currentFief.ownerID, out gameState);
					break;
				case MainActions.ArmyAdjustCombatValues:
					executionSuccess = AdjustCombatValues(arguments[0], byte.Parse(arguments[1]), byte.Parse(arguments[2])).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.ArmyAttack:
					executionSuccess = Attack(arguments[0], arguments[1]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.SiegeBesiegeCurrentFief:
					executionSuccess = BesiegeCurrentFief(arguments[0]).ResponseType == DisplayMessages.Success;
					if (executionSuccess)
						AddEnemy(gameState, currentFief.ownerID, out gameState);
					break;
				case MainActions.SiegeStorm:
					executionSuccess = SiegeRoundStorm(arguments[0]).ResponseType == DisplayMessages.None;
					break;
				case MainActions.SiegeNegotiation:
					executionSuccess = SiegeRoundNegotiate(arguments[0]).ResponseType == DisplayMessages.None;
					break;
				case MainActions.SiegeReduction:
					executionSuccess = SiegeRoundReduction(arguments[0]).ResponseType == DisplayMessages.None;
					break;
				case MainActions.SiegeEnd:
					executionSuccess = EndSiege(arguments[0]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.NPCmoveDirection:
					if (!Enum.TryParse(arguments[0], out MoveDirections direction))
						throw new Exception("Couldn't parse '" + arguments[0] + "' to a MoveDirections object");
					executionSuccess = Move(direction).ResponseType == DisplayMessages.Success;
					if (executionSuccess)
                    {
						nbConsecutiveNPCmoveDirections++;
						lastNPCmoveDirection = direction;
					}
					break;
				case MainActions.NPCmoveToFief:
					executionSuccess = MoveToFief(arguments[0]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.NPCmarryPC:
					respType = Marry(currentPCid, arguments[0]).ResponseType;
					executionSuccess = respType == DisplayMessages.Success || respType == DisplayMessages.CharacterProposalAlready;
					// The proposer is the PC here, not arguments[0] (the bride). Recording the
					// bride meant the PC was never marked as having proposed, so he re-proposed
					// to the same woman every pass; and Add() threw once the key was present.
					if (executionSuccess)
					{
						MarkProposed(gameState, currentPCid);
						InvalidateForeignChar(arguments[0]);
					}
					break;
				case MainActions.NPCmarryChild:
					respType = Marry(arguments[0], arguments[1]).ResponseType;
					executionSuccess = respType == DisplayMessages.Success || respType == DisplayMessages.CharacterProposalAlready;
					if (executionSuccess)
					{
						MarkProposed(gameState, arguments[0]);
						InvalidateForeignChar(arguments[1]);
					}
					break;
				case MainActions.NPCacceptRejectProposal:
					ProtoMessage proposalReply = AcceptRejectProposal(gameState.receivedProposalJEntryID, bool.Parse(arguments[0]));
					executionSuccess = proposalReply.ResponseType == DisplayMessages.Success;
					if (!executionSuccess)
						LastActionReason = proposalReply.ResponseType.ToString();
					if (executionSuccess)
					{
						AddAlly(gameState, gameState.receivedProposalFromThisChar.ownerName, out gameState); // The family of my wife naturally becomes an ally if not already
					}
					break;
				case MainActions.NPCtryForChild:
					respType = TryForChild().ResponseType;
					executionSuccess = respType == DisplayMessages.CharacterSpousePregnant || respType == DisplayMessages.CharacterSpouseNotPregnant || respType == DisplayMessages.CharacterSpouseNeverPregnant;
					break;
				case MainActions.NPChire:
					respType = hireNPC(arguments[0], uint.Parse(arguments[1])).ResponseType;
					// Only an accepted bid is a success. CharacterOfferLow/Almost/Haggle all mean
					// the man is still not ours, and counting them as successes wiped the failure
					// memory, so the same rejected number went back out every pass and the season
					// was spent haggling with one NPC who never said yes.
					executionSuccess = respType == DisplayMessages.CharacterOfferHigh
									|| respType == DisplayMessages.CharacterOfferOk;
					if (executionSuccess)
					{
						lastOfferToNPCid = null;
						lastOfferToNPCamount = 0;
					}
					else
					{
						lastOfferToNPCid = arguments[0];
						lastOfferToNPCamount = uint.Parse(arguments[1]);
						LastActionReason = respType.ToString();
						// Turning down a bid is how the engine haggles, not a sign the agent
						// picked wrongly, so these are counted apart from real refusals.
						LastActionWasNegotiation = respType == DisplayMessages.CharacterOfferLow
											   || respType == DisplayMessages.CharacterOfferAlmost
											   || respType == DisplayMessages.CharacterOfferHaggle;
						Console.ForegroundColor = ConsoleColor.DarkGray;
						Console.WriteLine($"  [HIRE] {arguments[0]} turned down {arguments[1]}: {respType}");
						Console.ResetColor();
					}
					// The offer we just made is part of his details, and the next one has to beat
					// it. Re-read him rather than let the season cache hand back the old figure -
					// otherwise the bid never rises and we haggle against a stale number forever.
					InvalidateForeignChar(arguments[0]);
					break;
				case MainActions.NPCfire:
					executionSuccess = fireNPC(arguments[0]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.NPCbarCharacters:
					executionSuccess = BarCharacters(arguments[0], arguments.Skip(1).ToArray()).ResponseType == DisplayMessages.None;
					break;
				case MainActions.NPCunbarCharacters:
					executionSuccess = UnbarCharacters(arguments[0], arguments.Skip(1).ToArray()).ResponseType == DisplayMessages.None;
					break;
				case MainActions.NPCbarNationalities:
					executionSuccess = BarNationalities(arguments[0], arguments.Skip(1).ToArray()).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.NPCunbarNationalities:
					executionSuccess = UnbarNationalities(arguments[0], arguments.Skip(1).ToArray()).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.NPCtryEnterKeep:
					respType = EnterExitKeep(arguments[0]).ResponseType;
					executionSuccess = respType == DisplayMessages.Success || respType == DisplayMessages.CharacterAlreadyArmy;
					break;
				case MainActions.NPCexitKeep:
					executionSuccess = EnterExitKeep(arguments[0]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.NPCcamp:
					var campReply = Camp(arguments[0], byte.Parse(arguments[1]));
					executionSuccess = campReply.ResponseType == DisplayMessages.CharacterCamp
					                || campReply.ResponseType == DisplayMessages.Success;
					if (!executionSuccess)
						Console.WriteLine($"  [Camp] Server rejected camp. ResponseType={campReply.ResponseType} | Msg='{campReply.Message}'");
					break;
				case MainActions.NPCaddToEntourage:
					executionSuccess = AddRemoveEntourage(arguments[0]).ResponseType == DisplayMessages.Success;
					if (executionSuccess)
						entouragedThisSeasonIDs.Add(arguments[0]); // don't re-add this NPC for the rest of the season
					break;
				case MainActions.NPCremoveFromEntourage:
					executionSuccess = AddRemoveEntourage(arguments[0]).ResponseType == DisplayMessages.Success;
					if (executionSuccess)
						entouragedThisSeasonIDs.Remove(arguments[0]);
					break;
				case MainActions.NPCappointHeir:
					executionSuccess = AppointHeir(arguments[0]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.NPCappointBaillif:
					ProtoMessage bailiffReply = AppointBailiff(arguments[0], arguments[1]);
					executionSuccess = bailiffReply.ResponseType == DisplayMessages.Success;
					if (executionSuccess)
					{
						// It accepted, so whatever was wrong before is no longer wrong.
						bailiffRefusalCounts.Remove(arguments[0]);
					}
					else
					{
						// Drop the fief for the rest of the season and move on to the next one.
						// The refusal is the fief's, not the candidate's - the same NPC is
						// accepted elsewhere in the same season - so retrying with a different
						// NPC just burns the season.
						LastActionReason = bailiffReply.ResponseType.ToString();
						bailiffBlockedFiefIDs.Add(arguments[0]);
						bailiffRefusalCounts.TryGetValue(arguments[0], out int refusals);
						bailiffRefusalCounts[arguments[0]] = ++refusals;
						string forHowLong = refusals >= BailiffGiveUpAfter
							? $"refused {refusals}x - leaving it alone until season {(seasonsPlayed / BailiffRetryEvery + 1) * BailiffRetryEvery}"
							: "skipping this fief for the season";
						Console.ForegroundColor = ConsoleColor.DarkYellow;
						Console.WriteLine($"  [BAILIFF] {arguments[0]} refused {arguments[1]}: {bailiffReply.ResponseType} - {forHowLong}.");
						Console.ResetColor();
					}
					break;
				case MainActions.NPCremoveBaillif:
					executionSuccess = RemoveBailiff(arguments[0]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.FiefGrantFiefTitle:
					executionSuccess = GrantFiefTitle(arguments[0], arguments[1]).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.FiefAutoAdjustExpenditure:
					var adjResp = AutoAdjustExpenditure(arguments[0]).ResponseType;
					executionSuccess = adjResp == DisplayMessages.FiefExpenditureAdjusted
					                || adjResp == DisplayMessages.Success;
					expenditureAdjustedThisSeasonFiefIDs.Add(arguments[0]); // always track to prevent retrying same fief this season
					break;
				case MainActions.FiefTransferFunds:
					executionSuccess = TransferFunds(arguments[0], arguments[1], int.Parse(arguments[2])).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.FiefTransferFundsToPlayer:
					executionSuccess = TransferFundsToPlayer(arguments[0], int.Parse(arguments[1])).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.SpyFief:
					protoMessage = SpyFief(arguments[0], arguments[1]);
					respType = protoMessage.ResponseType;
					executionSuccess = respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpySuccess || respType == DisplayMessages.SpyFailDead || respType == DisplayMessages.SpyFailDetected || respType == DisplayMessages.SpyFail;
					if (respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpySuccess)
                    {
						fiefSpiedThisSeasonIDs.Add(arguments[0]);
						if (gameState.spiedFiefs.ContainsKey(arguments[0]))
							gameState.spiedFiefs.Remove(arguments[0]);
						gameState.spiedFiefs.Add(arguments[0], new RedProtoFief((ProtoFief)protoMessage));
					}
					if (respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpyFailDead || respType == DisplayMessages.SpyFailDetected)
                    {
						if (!gameState.tryGetFief(arguments[0], out RedProtoFief fief))
							throw new Exception("Couldn't find the fief '" + arguments[0] + "', it shouldn't happen");
						AddEnemy(gameState, fief.ownerID, out gameState);
                    }
					break;
				case MainActions.SpyCharacter:
					protoMessage = SpyCharacter(arguments[0], arguments[1]);
					respType = protoMessage.ResponseType;
					executionSuccess = respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpySuccess || respType == DisplayMessages.SpyFailDead || respType == DisplayMessages.SpyFailDetected || respType == DisplayMessages.SpyFail;
					if (respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpySuccess)
                    {
						charSpiedThisSeasonIDs.Add(arguments[0]);
						if (gameState.spiedCharacters.ContainsKey(arguments[0]))
							gameState.spiedCharacters.Remove(arguments[0]);
						RedProtoCharacter redProtoCharacter;
						if (protoMessage is ProtoPlayerCharacter)
							redProtoCharacter = new RedProtoPlayerCharacter((ProtoPlayerCharacter)protoMessage);
						else
							redProtoCharacter = new RedProtoNPC((ProtoNPC)protoMessage);
						gameState.spiedCharacters.Add(arguments[0], redProtoCharacter);
					}
					if (respType == DisplayMessages.Success || respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpyFailDead || respType == DisplayMessages.SpyFailDetected)
					{
						if (!gameState.tryGetCharacter(arguments[0], out RedProtoCharacter spiedCharacter))
							throw new Exception("Shouldn't be null");
						if(spiedCharacter is RedProtoPlayerCharacter)
							AddEnemy(gameState, spiedCharacter.charID, out gameState);
                        else
                        {
							RedProtoNPC spiedNPC = (RedProtoNPC)spiedCharacter;
							if(! string.IsNullOrWhiteSpace(spiedNPC.employerId))
								AddEnemy(gameState, spiedNPC.employerId, out gameState);
						}
					}
					break;
				case MainActions.SpyArmy:
					protoMessage = SpyArmy(arguments[0], arguments[1]);
					respType = protoMessage.ResponseType;
					executionSuccess = respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpySuccess || respType == DisplayMessages.SpyFailDead || respType == DisplayMessages.SpyFailDetected || respType == DisplayMessages.SpyFail;
					if (respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpySuccess)
                    {
						armySpiedThisSeasonIDs.Add(arguments[0]);
						if (gameState.spiedArmies.ContainsKey(arguments[0]))
							gameState.spiedArmies.Remove(arguments[0]);
						gameState.spiedArmies.Add(arguments[0], new RedProtoArmy((ProtoArmy)protoMessage));
					}
					if (respType == DisplayMessages.SpySuccessDetected || respType == DisplayMessages.SpyFailDead || respType == DisplayMessages.SpyFailDetected)
					{
						if (!gameState.tryGetArmy(arguments[0], out RedProtoArmy spiedArmy))
							throw new Exception("Shouldn't be null");
						AddEnemy(gameState, spiedArmy.ownerName, out gameState);
					}
					break;
				case MainActions.KidnapCharacter:
					executionSuccess = Kidnap(arguments[0], arguments[1]) == null;
					if (executionSuccess) captiveCache.Clear();
					// TO DO: add ennemy if got detected
					break;
				case MainActions.KidnapRansomCaptive:
					executionSuccess = RansomCaptive(arguments[0]).ResponseType == DisplayMessages.Success;
                    if (executionSuccess)
                    {
						captiveCache.Clear();
						if (!gameState.tryGetNotMyPC(arguments[0], out RedProtoPlayerCharacter captiveRansomed))
							throw new Exception("Shouldn't be null");
						AddEnemy(gameState, captiveRansomed.charID, out gameState);
					}
					break;
				case MainActions.KidnapExecuteCaptive:
					executionSuccess = ExecuteCaptive(arguments[0]).ResponseType == DisplayMessages.Success;
					if (executionSuccess) captiveCache.Clear();
					break;
				case MainActions.KidnapReleaseCaptive:
					executionSuccess = ReleaseCaptive(arguments[0]).ResponseType == DisplayMessages.Success;
					if (executionSuccess)
					{
						captiveCache.Clear();
						charToReleaseIDs.Remove(arguments[0]);
					}
					break;
				case MainActions.KidnapRespondRansom:
					executionSuccess = RespondRansom(uint.Parse(arguments[0]), bool.Parse(arguments[1])).ResponseType == DisplayMessages.Success;
					break;
				case MainActions.None:
					throw new Exception("The action " + actionToExecute.ToString() + " isn't a valid one");
				default:
					throw new Exception("The action " + actionToExecute.ToString() + " isn't taken into account");
			}

            if (executionSuccess)
            {
                switch (actionToExecute)
                {
					case MainActions.NPCmoveDirection:
						if (!Enum.TryParse(arguments[0], out MoveDirections direction))
							throw new Exception("Couldn't parse '" + arguments[0] + "' to a MoveDirections object");
						if (!dayCostToTravelToAdjacentFiefs.TryGetValue(direction, out double costToTravelToAdjacentFief))
							throw new Exception("Couldn't find the key '" + actionToExecute.ToString() + "', it shouldn't happen");
						estimationNbDaysUsed += costToTravelToAdjacentFief;
						break;
					case MainActions.NPCmoveToFief:
						if(!dayCostToTravelToKnownFiefs.TryGetValue(arguments[0], out double dayCostToKnownFief))
							Console.WriteLine("Couldn't find the key '" + arguments[0] + "', the cost wasn't taken into account");
						estimationNbDaysUsed += dayCostToKnownFief;
						break;
					default:
						if (!maxDaysToDoAction.TryGetValue(actionToExecute, out double maxDaysToDoActionValue))
							throw new Exception("The key '" + actionToExecute.ToString() + "' isn't present in this dictionnary, it needs to be added!");
						estimationNbDaysUsed += maxDaysToDoActionValue * 0.75;
						break;
                }
				if (actionToExecute != MainActions.NPCmoveDirection)
					nbConsecutiveNPCmoveDirections = 0;
			}
			gameStateWithModifs = gameState;
			return executionSuccess;
		}

		private void AddAlly(GameState gameState, string charId, out GameState gameStateWithModifs)
        {
			gameState.allyPCids.Add(charId);
			gameState.enemyPCids.Remove(charId);
			gameStateWithModifs = gameState;
		}

		private void AddEnemy(GameState gameState, string charId, out GameState gameStateWithModifs)
		{
			gameState.enemyPCids.Add(charId);
			gameState.allyPCids.Remove(charId);
			gameStateWithModifs = gameState;
		}

		/// <summary>
		///     Search if a char can be barred from one of my fiefs
		/// </summary>
		protected bool CanBarCharFromOneOfMyFiefs(GameState gameState, string charID, out RedProtoFief fief)
		{
			fief = null;
			foreach (RedProtoFief myFief in gameState.myFiefs)
            {
				bool isBarredInThisFief = false;
				if(myFief.barredCharactersId != null)
					foreach (string barredCharId in myFief.barredCharactersId)
						if (barredCharId.Equals(charID))
						{
							isBarredInThisFief = true;
							break;
						}
				if (!isBarredInThisFief)
				{
					fief = myFief;
					return true;
				}
			}
			return false;
		}

		protected uint calculMyTotalNbTroops(GameState gameState)
		{
			uint myTotalNbTroops = 0;
			foreach (RedProtoArmy myArmy in gameState.myArmies)
				myTotalNbTroops += Tools.CalculSumUintArray(myArmy.troops);
			return myTotalNbTroops;
		}

		/// <summary>
		///     Estimates the acceptable salary limit for an NPC
		/// </summary>
		/// <param name="npcToHire">npc from which to estimate the acceptable salary limit</param>
		/// <returns>The acceptable salary limit</returns>
		protected double estimateNPCacceptableSalary(RedProtoNPC npcToHire)
		{
			double basicSalary = 1500;
			double maxRating = Math.Max(Tools.GetLeadershipValue(npcToHire), npcToHire.management);
			double minRating = Math.Min(Tools.GetLeadershipValue(npcToHire), npcToHire.management);

			// calculate potential salary, based on highest rating
			double fromRatings = minRating == 0
				? Math.Max(basicSalary, basicSalary * maxRating)
				: Math.Max(basicSalary, basicSalary * (maxRating + minRating / 2));

			// A man already drawing a wage will not take less than a raise on it, and the engine
			// prices him at the HIGHER of his traits and his current pay. Leaving this term out
			// under-priced everyone already in service, so the bid opened below the floor at
			// which offers are dismissed out of hand and the haggle never caught up: nineteen
			// hire attempts in twenty were refused across the long runs.
			double fromCurrent = npcToHire.salary > 0
				? npcToHire.salary * 1.05 * 1.11   // the server's own +5% then +11% to the median
				: 0;

			return Math.Max(fromRatings, fromCurrent);
		}
	}
}
