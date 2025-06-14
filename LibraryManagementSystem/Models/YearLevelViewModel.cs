using System.Collections.Generic;

namespace LibraryManagementSystem.Models
{
    public class YearLevelViewModel
    {
        public string YearLevel { get; set; }
        public List<SectionViewModel> Sections { get; set; } = new List<SectionViewModel>();
    }
} 