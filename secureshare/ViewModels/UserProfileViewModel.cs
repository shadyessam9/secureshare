using secureshare.Models;

namespace secureshare.ViewModels
{
    public class UserProfileViewModel
    {
        public User User { get; set; }
        public List<Folder> Folders { get; set; }
        public List<UserFolderPermission> Folderspermissions { get; set; }
        public List<Department> Departments { get; set; }

        public List<Branch> Branchs { get; set; }
        public List<Folder> UserFolders { get; set; }

    }
}
