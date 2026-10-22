using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
namespace KuaiYi.Core.Installation;

internal enum GenerationPreparationStage { SourceVerified,IntentWritten,SourceCopied,MetadataWritten,StagingVerified,DirectoryMoved,FinalVerified,BeforeCredential,BeforeQualification,AfterQualification }
internal sealed class InstallationPreparationFaults
{
    internal Action<string,string,IDisposable>? Acquired {get;init;}
    internal Action<string,string>? BeforeClose {get;set;}
    internal Action<string,FileStream,ReadOnlyMemory<byte>>? WriteMetadata {get;init;}
    internal Action<string,FileStream,ReadOnlyMemory<byte>>? WritePayload {get;init;}
    internal Action<string,FileStream>? BeforeFlush {get;init;}
    internal Action<GenerationPreparationStage>? Stage {get;init;}
    internal Action<string,string>? Move {get;init;}
}
// One scope owns this object before its original preparation Task starts. No legacy CopyTo/WriteFlushed.
internal sealed class InstallationPreparationIO(string source,string staging,string destination,string intent,CancellationToken cancellation,InstallationPreparationFaults? faults)
{
    private sealed class Resource(InstallationPreparationIO owner,string kind,string path)
    {internal readonly InstallationPreparationIO Owner=owner;internal readonly string Kind=kind,Path=path;internal IDisposable? Original;}
    private sealed record Metadata(InstallationPreparationIO Owner,string Path,byte[] Bytes);
    private sealed class Ledger
    {internal readonly object Sync=new();internal readonly List<Resource> Resources=[];internal readonly List<Metadata> Metadata=[];internal bool StorageUnconfirmed;}
    private Ledger ledger=new();
    private object sync=>ledger.Sync;
    private List<Resource> resources=>ledger.Resources;
    private List<Metadata> metadata=>ledger.Metadata;
    private bool storageUnconfirmed{get=>ledger.StorageUnconfirmed;set=>ledger.StorageUnconfirmed=value;}
    private string? additionalReadRoot;
    private bool metadataOnly;
    internal bool StorageUnconfirmed=>storageUnconfirmed;
    internal bool CleanupConfirmed {get{lock(sync)return resources.Count==0;}}
    private sealed class Operation(InstallationPreparationIO owner)
    {
        internal readonly List<Resource> Resources=[];
        internal T Own<T>(string kind,string path,Func<T> acquire)where T:class,IDisposable
        {
            var record=new Resource(owner,kind,path);Resources.Add(record);lock(owner.sync)owner.resources.Add(record);
            var value=acquire();record.Original=value;owner.faults?.Acquired?.Invoke(kind,path,value);return value;
        }
    }
    // Explicit fields allow the nested operation to share the exact original owner, never a recreated stream.
    private readonly InstallationPreparationFaults? faults=faults;
    private T Execute<T>(Func<Operation,T> body)
    {
        if(!CleanupConfirmed)throw new InstallationRecoveryException("Original preparation resources remain unsettled.");
        var operation=new Operation(this);T value=default!;var errors=new List<Exception>();
        try{value=body(operation);}catch(Exception error){errors.Add(error);}
        foreach(var resource in operation.Resources.AsEnumerable().Reverse())try{Close(resource);}catch(Exception error){errors.Add(error);}
        InstallationErrors.Throw(errors);return value;
    }
    private void Close(Resource resource)
    {
        if(resource.Original!=null){resource.Owner.faults?.BeforeClose?.Invoke(resource.Kind,resource.Path);resource.Original.Dispose();resource.Original=null;}
        lock(sync)resources.Remove(resource);
    }
    internal void CloseOriginals()
    {
        Resource[] originals;lock(sync)originals=resources.ToArray();var errors=new List<Exception>();
        foreach(var original in originals.Reverse())try{Close(original);}catch(Exception error){errors.Add(error);}
        InstallationErrors.Throw(errors);
    }
    private void Readable(string path)
    {
        path=Path.GetFullPath(path);
        if(!InstallationPaths.Contains(source,path)&&!InstallationPaths.Contains(staging,path)&&!InstallationPaths.Contains(destination,path)&&path!=intent&&(additionalReadRoot==null||!InstallationPaths.Contains(additionalReadRoot,path)))throw new InvalidDataException("Read is outside the original preparation paths.");
        InstallationPaths.Check(path);
    }
    private void Writable(string path)
    {if(path!=intent&&(metadataOnly||!InstallationPaths.Contains(staging,Path.GetFullPath(path))))throw new InvalidDataException("Write is outside the original inactive candidate.");InstallationPaths.Check(path);}
    private FileStream Open(Operation operation,string kind,string path,FileMode mode,FileAccess access)
    {
        if(access==FileAccess.Read)Readable(path);else Writable(path);
        try{return operation.Own(kind,path,()=>new FileStream(path,mode,access,FileShare.None));}
        catch{if(access!=FileAccess.Read)storageUnconfirmed=true;throw;}
    }
    internal byte[] ReadDocument(string path,int maximum)=>ReadDocument(path,maximum,cancellation);
    private byte[] ReadDocument(string path,int maximum,CancellationToken token)
    {
        if(maximum is <1 or >4*1024*1024)throw new InvalidDataException("Preparation document bound invalid.");
        return Execute(operation=>
        {
            token.ThrowIfCancellationRequested();var input=Open(operation,"read",path,FileMode.Open,FileAccess.Read);
            long length=input.Length;if(length is <1||length>maximum)throw new InvalidDataException("Preparation metadata size invalid.");
            var bytes=new byte[(int)length];for(int offset=0;offset<bytes.Length;offset+=65536){token.ThrowIfCancellationRequested();input.ReadExactly(bytes.AsSpan(offset,Math.Min(65536,bytes.Length-offset)));}
            if(input.Length!=length)throw new InvalidDataException("Preparation metadata changed while reading.");return bytes;
        });
    }
    internal IEnumerable<string> Entries(string directory)
    {
        Readable(directory);return Execute(operation=>
        {
            var enumerator=operation.Own("enumerator",directory,()=>Directory.EnumerateFileSystemEntries(directory).GetEnumerator());var result=new List<string>();
            while(enumerator.MoveNext()){cancellation.ThrowIfCancellationRequested();if(result.Count>=4096*17)throw new InvalidDataException("Preparation directory exceeds its bound.");result.Add(enumerator.Current);}return result.ToArray();
        });
    }
    internal void ReadPe(string path,Action<PEReader> inspect)
    {
        _=Execute(operation=>
        {
            cancellation.ThrowIfCancellationRequested();var input=Open(operation,"pe-input",path,FileMode.Open,FileAccess.Read);
            var reader=operation.Own("pe-reader",path,()=>new PEReader(input,PEStreamOptions.LeaveOpen));inspect(reader);return true;
        });
    }
    internal void VerifyFile(string path,InstallationFile entry,CancellationToken token)
    {_=Execute(operation=>{var input=Open(operation,"verify",path,FileMode.Open,FileAccess.Read);Transfer(operation,input,null,path,entry,token);return true;});}
    internal string HashFile(string path)=>Execute(operation=>
    {
        cancellation.ThrowIfCancellationRequested();var input=Open(operation,"hash-input",path,FileMode.Open,FileAccess.Read);long length=input.Length;
        if(length is <=0 or >32*1024*1024)throw new InvalidDataException("Bootstrap hash input exceeds bound.");
        var hash=operation.Own("hash",path,()=>IncrementalHash.CreateHash(HashAlgorithmName.SHA256));byte[] buffer=new byte[65536];long total=0;int count;
        while((count=input.Read(buffer))!=0){cancellation.ThrowIfCancellationRequested();total+=count;if(total>length)throw new InvalidDataException("Bootstrap hash input grew.");hash.AppendData(buffer,0,count);}
        if(total!=length||input.Length!=length)throw new InvalidDataException("Bootstrap hash input changed.");return Convert.ToHexString(hash.GetHashAndReset());
    });
    internal bool ContainsPayload(string path,ReadOnlySpan<byte> value)
    {
        if(value.Length is <1 or >256)throw new InvalidDataException("Bootstrap binding search is unbounded.");byte[] needle=value.ToArray();
        return Execute(operation=>
        {
            cancellation.ThrowIfCancellationRequested();var input=Open(operation,"binding-input",path,FileMode.Open,FileAccess.Read);long length=input.Length;
            if(length is <=0 or >32*1024*1024)throw new InvalidDataException("Bootstrap apphost exceeds bound.");
            byte[] buffer=new byte[65536+needle.Length-1];int retained=0,count;long total=0;bool found=false;
            while((count=input.Read(buffer,retained,65536))!=0)
            {
                cancellation.ThrowIfCancellationRequested();total+=count;if(total>length)throw new InvalidDataException("Bootstrap apphost grew.");int available=retained+count;found|=buffer.AsSpan(0,available).IndexOf(needle)>=0;
                retained=Math.Min(needle.Length-1,available);buffer.AsSpan(available-retained,retained).CopyTo(buffer);
            }
            if(total!=length||input.Length!=length)throw new InvalidDataException("Bootstrap apphost changed.");return found;
        });
    }
    private void Transfer(Operation operation,FileStream input,FileStream? output,string path,InstallationFile entry,CancellationToken token)
    {
        if(input.Length!=entry.Bytes)throw new InvalidDataException("Preparation payload length changed.");
        var hash=operation.Own("hash",path,()=>IncrementalHash.CreateHash(HashAlgorithmName.SHA256));byte[] buffer=new byte[65536];long total=0;int count;
        while((count=input.Read(buffer))!=0)
        {
            token.ThrowIfCancellationRequested();total+=count;if(total>entry.Bytes)throw new InvalidDataException("Preparation payload grew.");hash.AppendData(buffer,0,count);
            if(output!=null){if(faults?.WritePayload!=null)faults.WritePayload(path,output,buffer.AsMemory(0,count));else output.Write(buffer,0,count);}
        }
        if(total!=entry.Bytes||!string.Equals(Convert.ToHexString(hash.GetHashAndReset()),entry.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Preparation payload digest changed.");
        if(output!=null){faults?.BeforeFlush?.Invoke(path,output);output.Flush(true);}
    }
    internal void CopyPayload(string from,string to,InstallationFile entry)
    {
        if(metadataOnly)throw new InvalidOperationException("Prepared channel cannot copy payload.");
        RequireSettled();
        Writable(to);Directory.CreateDirectory(Path.GetDirectoryName(to)!);InstallationPaths.Check(Path.GetDirectoryName(to)!);
        _=Execute(operation=>
        {
            cancellation.ThrowIfCancellationRequested();var input=Open(operation,"copy-input",from,FileMode.Open,FileAccess.Read);var output=Open(operation,"copy-output",to,FileMode.CreateNew,FileAccess.Write);
            Transfer(operation,input,output,to,entry,cancellation);return true;
        });
    }
    internal void WriteMetadata(string path,byte[] bytes,int maximum)
    {
        if(bytes.Length is <1||bytes.Length>maximum)throw new InvalidDataException("Preparation metadata exceeds consumer bound.");
        byte[] expected=bytes.ToArray();bool attempted=false;
        try
        {
            _=Execute(operation=>
            {
                cancellation.ThrowIfCancellationRequested();attempted=true;var output=Open(operation,"metadata-output",path,FileMode.CreateNew,FileAccess.Write);
                if(faults?.WriteMetadata!=null)faults.WriteMetadata(path,output,expected);
                else for(int offset=0;offset<expected.Length;offset+=65536){cancellation.ThrowIfCancellationRequested();output.Write(expected.AsSpan(offset,Math.Min(65536,expected.Length-offset)));}
                faults?.BeforeFlush?.Invoke(path,output);output.Flush(true);return true;
            });
            metadata.Add(new(this,path,expected));
        }
        catch{if(attempted)storageUnconfirmed=true;throw;}
    }
    internal void RequireExactMetadata(string path,byte[] expected,int maximum)
    {
        try{if(!ReadDocument(path,maximum).AsSpan().SequenceEqual(expected))throw new InstallationRecoveryException("Preparation metadata changed; retained.");}
        catch{storageUnconfirmed=true;throw;}
    }
    internal void VerifyRecordedMetadata()
    {
        // Called by the prepublished finalizer after joining preparation. New reads use the same ledger and ignore late cancellation.
        foreach(var record in metadata.ToArray())
            try{if(!record.Owner.ReadDocument(record.Path,Math.Max(record.Bytes.Length,1),CancellationToken.None).AsSpan().SequenceEqual(record.Bytes))throw new InstallationRecoveryException("Inactive candidate metadata changed; retained.");}
            catch{storageUnconfirmed=true;throw;}
    }
    internal void RequireAbsent(string path)
    {
        InstallationPaths.Check(path);
        try{_=File.GetAttributes(path);storageUnconfirmed=true;throw new InstallationRecoveryException("Unknown candidate path retained.");}
        catch(FileNotFoundException){var parent=File.GetAttributes(Path.GetDirectoryName(path)!);if((parent&FileAttributes.Directory)==0)throw new DirectoryNotFoundException("Candidate parent is unconfirmed.");}
    }
    internal void CreateStaging()
    {
        if(metadataOnly)throw new InvalidOperationException("Prepared channel cannot create staging.");
        RequireSettled();
        RequireAbsent(staging);
        try{Directory.CreateDirectory(staging);InstallationPaths.Check(staging);}catch{storageUnconfirmed=true;throw;}
    }
    internal void MoveToFinal()
    {
        if(metadataOnly)throw new InvalidOperationException("Prepared channel cannot move directories.");
        RequireSettled();
        string versions=Path.GetDirectoryName(destination)!;InstallationPaths.Check(versions);Directory.CreateDirectory(versions);InstallationPaths.Check(versions);RequireAbsent(destination);
        try{if(faults?.Move!=null)faults.Move(staging,destination);else Directory.Move(staging,destination);}
        catch{storageUnconfirmed=true;throw;}
        for(int i=0;i<metadata.Count;i++)if(ReferenceEquals(metadata[i].Owner,this)&&InstallationPaths.Contains(staging,metadata[i].Path))metadata[i]=metadata[i] with{Path=Path.Combine(destination,Path.GetRelativePath(staging,metadata[i].Path))};
    }
    // The original G scope lends a channel, not a new resource ledger or independent cleanup authority.
    internal InstallationPreparationIO CreateBootstrapChannel(string bootstrapSource,string bootstrapStaging,string bootstrapDestination,string bootstrapIntent,InstallationPreparationFaults? bootstrapFaults)
    {RequireSettled();return new(bootstrapSource,bootstrapStaging,bootstrapDestination,bootstrapIntent,cancellation,bootstrapFaults){ledger=ledger,additionalReadRoot=destination};}
    internal InstallationPreparationIO CreatePreparedChannel(string root,string plan,InstallationPreparationFaults? preparedFaults)
    {RequireSettled();return new(root,root,root,plan,cancellation,preparedFaults){ledger=ledger,metadataOnly=true};}
    internal void MarkUnconfirmed()=>storageUnconfirmed=true;
    internal void RequireDirectory(string path)
    {Readable(path);var attributes=File.GetAttributes(path);if((attributes&(FileAttributes.Directory|FileAttributes.ReparsePoint|FileAttributes.Device))!=FileAttributes.Directory)throw new InstallationRecoveryException("Owned container is not a regular directory.");}
    internal void Stage(GenerationPreparationStage stage)=>faults?.Stage?.Invoke(stage);
    private void RequireSettled(){if(!CleanupConfirmed)throw new InstallationRecoveryException("Original preparation resources remain unsettled.");cancellation.ThrowIfCancellationRequested();}
}
