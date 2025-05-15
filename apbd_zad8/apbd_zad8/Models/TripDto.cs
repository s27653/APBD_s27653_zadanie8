using System;
using System.Collections.Generic;

namespace apbd_zad8.Models
{
    public class TripDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime DateFrom { get; set; } 
        public DateTime DateTo { get; set; }
        public int MaxPeople { get; set; }
        public List<string> Countries { get; set; }
    }
}