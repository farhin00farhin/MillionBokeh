using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MillionBokeh.Models
{
    public class AuthenticationInfoViewModel
    {

        // Login Viewmodel Properties
        [Required]
        [Display(Name = "Email")]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }




        // Register Viewmodel Properties

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        //[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }


        //[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string Profile { get; set; }
        public short? MembershipType { get; set; }
        public int? RangeId { get; set; }

        [DataType(DataType.Upload)]
        public HttpPostedFileBase ImageAttachment { get; set; }

        public IEnumerable<Location> Locations { get; set; }

    }
}