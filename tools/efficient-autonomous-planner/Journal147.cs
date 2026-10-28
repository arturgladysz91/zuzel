using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SharedWorldSpeedway;
namespace Efficient147;
/// <summary>Write-ahead complete JSON lines, hash chained and fsynced before the chooser returns a command.</summary>
public sealed class Journal147:IDisposable{
    readonly FileStream file;readonly JsonSerializerOptions json=new(FreeLearningExperiment.Json){WriteIndented=false};string previous="GENESIS";int index;
    public Journal147(string path)=>file=new(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
    public void Append(object payload){
        var text=JsonSerializer.Serialize(payload,json);var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous+text)));
        var line=$"{{\"Index\":{index},\"Previous\":\"{previous}\",\"Payload\":{text},\"Sha256\":\"{hash}\"}}\n";
        file.Write(Encoding.UTF8.GetBytes(line));file.Flush(true);previous=hash;index++;
    }
    public static JsonElement[] ReadComplete(string path){
        var text=File.ReadAllText(path,Encoding.UTF8);var stop=text.LastIndexOf('\n');if(stop<0)return Array.Empty<JsonElement>();
        var rows=new List<JsonElement>();string previous="GENESIS";int index=0;
        foreach(var line in text[..stop].Split('\n',StringSplitOptions.RemoveEmptyEntries)){
            using var doc=JsonDocument.Parse(line);var e=doc.RootElement;var payload=e.GetProperty("Payload");
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous+payload.GetRawText())));
            if(e.GetProperty("Index").GetInt32()!=index++||e.GetProperty("Previous").GetString()!=previous||e.GetProperty("Sha256").GetString()!=hash)throw new IOException("Decision journal chain differs.");
            rows.Add(payload.Clone());previous=hash;
        }
        return rows.ToArray();
    }
    public void Dispose()=>file.Dispose();
}
