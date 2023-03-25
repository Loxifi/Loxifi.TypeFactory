using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestBinaries
{
	[TestAttribute]
	public class BaseClass
	{
		[TestAttribute]
		public string PropertyA { get; set; }
		public string PropertyB { get; set; }
		public string PropertyC { get; set; }
	}
}
