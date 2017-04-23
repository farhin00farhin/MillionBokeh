//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MillionBokeh.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Product
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Product()
        {
            this.Reviews = new HashSet<Review>();
        }
    
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Subtitle { get; set; }
        public int CategoryID { get; set; }
        public decimal UnitPrice { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public string ProductInfo { get; set; }
        public int AvailableUnits { get; set; }
    
        public virtual Category Category { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Review> Reviews { get; set; }
    }
}