﻿using RestaurantManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.Models
{
    public class Review
    {
        public int ReviewID { get; set; }
        public int CustomerID { get; set; }
        public int RestaurantID { get; set; }
        public int Rating { get; set; } // Range: 1 to 5
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser? Customer { get; set; }
        public Restaurant? Restaurant { get; set; }
    }

}
