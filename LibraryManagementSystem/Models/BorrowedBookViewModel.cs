namespace LibraryManagementSystem.Models
{
    public class BorrowedBookViewModel
    {
        public string Title { get; set; }
        public DateTime BorrowedDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; }
    }
}
