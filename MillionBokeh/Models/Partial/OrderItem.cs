using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MillionBokeh.Models
{
    public partial class OrderItem
    {
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public decimal ItemPrice { get; set; }
        public string ItemImage { get; set; }
        public int ItemsAvailable { get; set; }

    }
}