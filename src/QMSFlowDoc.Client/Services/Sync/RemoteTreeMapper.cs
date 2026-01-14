using System;
using System.Collections.Generic;
using System.Linq;

namespace QMSFlowDoc.Client.Services.Sync;

public class RemoteTreeMapper
{
    private readonly Dictionary<string, RemoteFile> _filesById;
    private readonly Dictionary<string, List<RemoteFile>> _childrenByParentId;

    public RemoteTreeMapper(List<RemoteFile> allFiles)
    {
        _filesById = allFiles.ToDictionary(f => f.Id, f => f);
        _childrenByParentId = allFiles
            .Where(f => !string.IsNullOrEmpty(f.ParentId))
            .GroupBy(f => f.ParentId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public string BuildRelativePath(string fileId, string rootId)
    {
        if (!_filesById.TryGetValue(fileId, out var file)) return "";
        
        var pathParts = new Stack<string>();
        pathParts.Push(file.Name);

        var current = file;
        while (!string.IsNullOrEmpty(current.ParentId) && current.ParentId != rootId)
        {
            if (_filesById.TryGetValue(current.ParentId, out var parent))
            {
                pathParts.Push(parent.Name);
                current = parent;
            }
            else
            {
                // Parent not found in scope or is disjoint
                break;
            }
        }
        
        return string.Join(System.IO.Path.DirectorySeparatorChar, pathParts);
    }
}
