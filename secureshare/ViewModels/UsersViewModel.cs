using secureshare.Models;

namespace secureshare.ViewModels
{
    public class UsersViewModel
    {
        public List<User> Users { get; set; }
        public List<Department> Departments { get; set; }

        public List<Branch> Branchs { get; set; }

        public List<Folder> Folders { get; set; }


    }
}
