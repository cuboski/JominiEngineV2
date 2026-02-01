using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;

namespace JominiEngine
{
    // holds an assortment of important values as strings which the old db had (cassandra needed a table and so this class allows to map any key value string pair)
    public class HeldStrings
    {
        /// <summary>
		/// Holds string id
		/// </summary>
        public string id { get; set; }
        /// <summary>
		/// Holds value
		/// </summary>
        public string value { get; set; }
        
        public HeldStrings()
        {

        }

        public HeldStrings(string id, string value)
        {
            this.id = id;
            this.value = value;
        }


    }

}
