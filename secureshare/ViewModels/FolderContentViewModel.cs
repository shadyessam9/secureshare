using System.Collections.Generic;

namespace secureshare.ViewModels
{
    public class FolderContentViewModel
    {
        public string FolderPath { get; set; }
        public IEnumerable<string> Files { get; set; }
        public IEnumerable<string> SubFolders { get; set; }

        public bool HasPermission { get; set; }

        public int UserPermissionType { get; set; }
    }
}

