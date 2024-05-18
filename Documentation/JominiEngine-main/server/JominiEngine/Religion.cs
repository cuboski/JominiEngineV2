using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JominiEngine
{
    public class Sect
    {
        public string id { get; set; }

        public Religion religion { get; set; }

        public int sectID { get; set; }

        public Sect(Religion rel, int sId)
        {
            if (sId < 1)
                throw new ArgumentException("sect id must be int > 0");

            this.religion = rel;
            this.sectID = sId;
            this.id = this.religion.id + this.sectID;

        }

    }

    public class Religion
    {
        public string id { get; set; }

        public string name { get; set; }

        public Religion(string religionID, string Name)
        {
            this.id = religionID;
            this.name = Name;
        }

        public Religion()
        {
        }
    }
}
