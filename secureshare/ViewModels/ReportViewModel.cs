using secureshare.Models;

namespace secureshare.ViewModels
{
    public class ReportViewModel
    {

        public int users { get; set; }

        public int folders { get; set; }


        public int Uploads { get; set; }

        public int Downloads { get; set; }

        public List<User> Users { get; set; }

        public List<Folder> Folders { get; set; }


        public List<UserFolderPermission> UFPS { get; set; }


        public List<Models.Action> Actions  { get; set; }
    }
}
