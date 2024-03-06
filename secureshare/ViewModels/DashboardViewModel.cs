using secureshare.Models;

namespace secureshare.ViewModels
{
    public class DashboardViewModel
    {

        public int Users { get; set; }

        public int Folders { get; set; }


        public int Uploads { get; set; }

        public int Downloads { get; set; }

        public List<Models.Action> Actions { get; set; }


        public List<User> users{ get; set; }


        public List<Folder> folders { get; set; }



    }
}
