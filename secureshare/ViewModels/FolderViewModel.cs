namespace secureshare.ViewModels
{
    public class FolderViewModel
    {
        // Ensure to fully qualify the usage of 'Action' from 'secureshare.Models'
        public int FolderID { get; set; }
        public string FolderName { get; set; }
        public string FolderPath { get; set; }
        public int FileCount { get; set; }
        // Specify the usage of 'secureshare.Models.Action' and 'secureshare.Models.UserFolderPermission'
        public ICollection<secureshare.Models.Action> Actions { get; set; }
        public ICollection<secureshare.Models.UserFolderPermission> UserFolderPermissions { get; set; }
        // Include other relevant collections or properties
    }
}
