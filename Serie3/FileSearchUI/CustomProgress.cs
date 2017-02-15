using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSearchUI {

    class CustomProgress {

        public readonly int total; // used to set the Maximum on the progressbar
        public readonly string file; // file to add to the list on each update

        public CustomProgress(int t, string f) {
            this.total = t;
            this.file = f;
        }

    }

}