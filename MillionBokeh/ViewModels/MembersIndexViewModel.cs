using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MillionBokeh.Models;

namespace MillionBokeh.ViewModels
{
    public class MembersIndexViewModel
    {
        public IEnumerable<AspNetUser> Users { get; set; }

        public IEnumerable<Location> Locations { get; set; }
    }
}