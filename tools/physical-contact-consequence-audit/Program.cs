using System.Collections;
using System.Reflection;
using System.Text.Json;
using CoreSim.Analysis;

Directory.CreateDirectory(args[0]);
if(args.Contains("--performance",StringComparer.Ordinal))
{
    File.WriteAllText(Path.Combine(args[0],"physical-contact-consequence-performance.json"),
        JsonSerializer.Serialize(PhysicalContactConsequenceEvidence.Performance(),PhysicalContactEvidence.JsonOptions)+"\n");
    return;
}
var trace=args.Contains("--trace",StringComparer.Ordinal);
var report=trace?PhysicalContactConsequenceEvidence.Trace()
    :PhysicalContactConsequenceEvidence.Report(!args.Contains("--controlled",StringComparer.Ordinal),Console.WriteLine);
var json=JsonSerializer.Serialize(report,PhysicalContactEvidence.JsonOptions).Replace("\r\n","\n",StringComparison.Ordinal)+"\n";
File.WriteAllText(Path.Combine(args[0],trace?"physical-contact-arithmetic.json":"physical-contact-consequences.json"),json);
var bits=new SortedDictionary<string,object>(StringComparer.Ordinal);Walk(report,"Report",bits);
File.WriteAllText(Path.Combine(args[0],trace?"physical-contact-arithmetic-bits.json":"physical-contact-consequence-bits.json"),
    JsonSerializer.Serialize(bits,PhysicalContactEvidence.JsonOptions).Replace("\r\n","\n",StringComparison.Ordinal)+"\n");
Console.WriteLine("#56C2 consequence evidence and typed IEEE bits written.");
static void Walk(object? value,string path,IDictionary<string,object> bits)
{
    if(value is float single){bits[path]=new{Type="float",Bits=$"0x{BitConverter.SingleToInt32Bits(single):X8}"};return;}
    if(value is double number){bits[path]=new{Type="double",Bits=$"0x{BitConverter.DoubleToInt64Bits(number):X16}"};return;}
    if(value is null||value is string||value.GetType().IsPrimitive||value.GetType().IsEnum)return;
    if(value is IEnumerable sequence){var index=0;foreach(var item in sequence)Walk(item,$"{path}[{index++}]",bits);return;}
    foreach(var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.GetIndexParameters().Length==0))
        Walk(property.GetValue(value),path+"."+property.Name,bits);
}
