namespace LibraryManagementSystem.Models
{
    public class BorrowingHistoryViewModel
    {
        public string Title { get; set; }
        public DateTime BorrowedDate { get; set; }
        public DateTime? ReturnedDate { get; set; }
    }
}
