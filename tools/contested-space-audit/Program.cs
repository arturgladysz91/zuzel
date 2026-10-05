using System.Collections;
using System.Reflection;
using System.Text.Json;
using CoreSim.Analysis;

Directory.CreateDirectory(args[0]);
File.WriteAllText(Path.Combine(args[0],"contested-space-racing-response.json"),
    ContestedSpaceResponseEvidence.DeterministicJson(progress:Console.Error.WriteLine).Replace("\r\n","\n",StringComparison.Ordinal));
var bits = new SortedDictionary<string,object>(StringComparer.Ordinal);
foreach (var scenario in ContestedSpaceResponseEvidence.Scenarios())
{
    var step = ContestedSpaceResponseEvidence.Resolve(scenario);
    // Raw production values remain separate from rounded #55 geometry evidence.
    Walk(new {step.Changes,step.Diagnostics,step.Motions,step.Events},scenario.Name,bits);
}
File.WriteAllText(Path.Combine(args[0],"production-motion-bits.json"),
    JsonSerializer.Serialize(bits,new JsonSerializerOptions {WriteIndented=true}).Replace("\r\n","\n",StringComparison.Ordinal)+"\n");
Console.WriteLine("#56B production report and typed physical IEEE captures written.");

static void Walk(object? value,string path,IDictionary<string,object> bits)
{
    if (value is float single) {bits[path]=new {Type="float",Bits=$"0x{BitConverter.SingleToInt32Bits(single):X8}"};return;}
    if (value is double number) {bits[path]=new {Type="double",Bits=$"0x{BitConverter.DoubleToInt64Bits(number):X16}"};return;}
    if (value is null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return;
    if (value is IEnumerable sequence)
    {var i=0;foreach(var item in sequence)Walk(item,$"{path}[{i++}]",bits);return;}
    foreach(var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.GetIndexParameters().Length==0))
        Walk(property.GetValue(value),path+"."+property.Name,bits);
}
