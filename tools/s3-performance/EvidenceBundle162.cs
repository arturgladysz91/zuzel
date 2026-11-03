using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LineThrottle156;
namespace S3Performance162;

/// <summary>One ordinary ZIP per native world. A single compressed JSONL entry
/// shares repeated structure across reviews. Index hashes cover exact UTF8 payloads.</summary>
public sealed class EvidenceBundle162:IDisposable {
 readonly ZipArchive archive;
 readonly Stream policies;
 readonly List<object> index=new();
 readonly JsonSerializerOptions json=new(Study156.Json){WriteIndented=false};
 bool closed;
 public EvidenceBundle162(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);archive=ZipFile.Open(path,ZipArchiveMode.Create);policies=archive.CreateEntry("policies.jsonl",CompressionLevel.Optimal).Open();}
 public void Policy(string key,object value){if(closed)throw new InvalidOperationException("Policy stream closed");var bytes=JsonSerializer.SerializeToUtf8Bytes(value,json);var name=JsonSerializer.SerializeToUtf8Bytes(key);policies.Write("{\"key\":"u8);policies.Write(name);policies.Write(",\"payload\":"u8);policies.Write(bytes);policies.Write("}\n"u8);index.Add(new{Key=key,Bytes=bytes.Length,SHA256=Convert.ToHexString(SHA256.HashData(bytes))});}
 public void Complete(object native,object result,object journal,object sources){if(closed)throw new InvalidOperationException("Evidence already completed");policies.Dispose();closed=true;Write("native.json",native);Write("result.json",result);Write("journal.json",journal);Write("sources.json",sources);Write("index.json",new{Format="S3Evidence162-v1",Policies=index,Integrity="SHA256 of exact compact UTF8 payload bytes in each policies.jsonl record; archive entry CRC and separate whole-archive SHA256. Evidence never substitutes independent recertification."});}
 void Write(string name,object value){var bytes=JsonSerializer.SerializeToUtf8Bytes(value,json);using var stream=archive.CreateEntry(name,CompressionLevel.Optimal).Open();stream.Write(bytes);}
 public void Dispose(){if(!closed){policies.Dispose();closed=true;}archive.Dispose();}
}
