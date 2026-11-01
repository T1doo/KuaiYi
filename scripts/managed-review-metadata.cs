using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
var rows=new List<object>();
foreach(string path in args)
{
    using var stream=File.OpenRead(path);using var pe=new PEReader(stream);
    if(!pe.HasMetadata)throw new InvalidDataException("Managed audit input required.");
    var md=pe.GetMetadataReader();var assembly=md.GetAssemblyDefinition();string? information=null;
    foreach(var handle in assembly.GetCustomAttributes())
    {
        var attr=md.GetCustomAttribute(handle);
        if(attr.Constructor.Kind!=HandleKind.MemberReference)continue;
        var member=md.GetMemberReference((MemberReferenceHandle)attr.Constructor);
        if(member.Parent.Kind!=HandleKind.TypeReference)continue;
        var type=md.GetTypeReference((TypeReferenceHandle)member.Parent);
        if(md.GetString(type.Namespace)!="System.Reflection"||md.GetString(type.Name)!="AssemblyInformationalVersionAttribute")continue;
        var blob=md.GetBlobReader(attr.Value);if(blob.ReadUInt16()!=1)throw new InvalidDataException("Attribute prolog differs.");
        information=blob.ReadSerializedString();
    }
    var resources=new List<object>();
    foreach(var handle in md.ManifestResources)
    {
        var resource=md.GetManifestResource(handle);if(!resource.Implementation.IsNil)throw new InvalidDataException("External resource refused.");
        var data=pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress).GetContent();int offset=checked((int)resource.Offset);
        int length=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset,4));
        if(length<0||length>4*1024*1024)throw new InvalidDataException("Resource exceeds audit bound.");
        resources.Add(new{Name=md.GetString(resource.Name),Bytes=length,Sha256=Convert.ToHexString(SHA256.HashData(data.AsSpan(offset+4,length))).ToLowerInvariant()});
    }
    rows.Add(new{Path=Path.GetFullPath(path),Name=md.GetString(assembly.Name),Version=assembly.Version.ToString(),Information=information,Resources=resources});
}
Console.WriteLine(JsonSerializer.Serialize(rows));
