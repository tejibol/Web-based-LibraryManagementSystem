using System.Collections.Generic;

namespace LibraryManagementSystem.Models
{
    public class SectionViewModel
    {
        public string SectionName { get; set; }
        public List<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
        
        // Helper property to get a display-friendly section name
        public string DisplayName 
        { 
            get 
            {
                if (string.IsNullOrEmpty(SectionName))
                    return "Unassigned";
                    
                if (SectionName.Contains("-"))
                {
                    string[] parts = SectionName.Split('-');
                    if (parts.Length >= 3)
                    {
                        string sectionNumber = parts[1].Replace("S", "Section ");
                        string sectionName = parts[2];
                        return $"{sectionNumber} - {sectionName}";
                    }
                }
                
                return SectionName;
            } 
        }
    }
} 