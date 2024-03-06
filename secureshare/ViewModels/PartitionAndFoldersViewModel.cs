using secureshare.Models;

namespace secureshare.ViewModels
{
    public class PartitionAndFoldersViewModel
    {
        public IEnumerable<Partition> Partitions { get; set; }
        public IEnumerable<Folder> Folders { get; set; }
    }
}
