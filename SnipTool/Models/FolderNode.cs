using System.Collections.ObjectModel;

namespace SnipTool.Models;

public sealed class FolderNode
{
    public FolderNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
        Children = new ObservableCollection<FolderNode>();
    }

    public string Name { get; }
    public string FullPath { get; }
    public ObservableCollection<FolderNode> Children { get; }
}
