using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MillionBokeh.Models
{
    public partial class Product
    {
        public short Rating { get; set; }
        public List<Product> Related { get; set; }
    }
}