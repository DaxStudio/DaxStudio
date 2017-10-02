using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.Controls.DataGridFilter.Querying
{
    public class ParameterCounter
    {
        public int ParameterNumber { get { return count - 1; } }

        private int count { get; set; }

        public void Increment()
        {
            count++;
        }

        public void Decrement()
        {
            count--;
        }

        public ParameterCounter()
        {
        }

        public ParameterCounter(int count)
        {
            this.count = count;
        }

        public override string ToString()
        {
            return ParameterNumber.ToString();
        }
    }
}
