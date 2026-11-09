using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Independent read-only comparison: product input is git objects, never the working tree.
string Git(params string[] args) {
    var p = new Process { StartInfo = new ProcessStartInfo("git") { WorkingDirectory = "C:/Dev/CoreKeeperModSDK-refactor-20261106", RedirectStandardOutput = true, UseShellExecute = false } };
    foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
    p.Start(); var value = p.StandardOutput.ReadToEnd(); p.WaitForExit();
    if (p.ExitCode != 0) throw new Exception("git failed"); return value;
}
string T(SyntaxNode n) => string.Join(" ", n.DescendantTokens().Select(t => t.Text));
string Id(BaseTypeDeclarationSyntax n) => string.Join(".", n.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(x => x.Name.ToString()).Concat(n.Ancestors().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(x => x.Identifier.Text)).Append(n.Identifier.Text));
Dictionary<string,List<(string file,BaseTypeDeclarationSyntax node)>> Read(string rev) {
    var result = new Dictionary<string,List<(string,BaseTypeDeclarationSyntax)>>();
    foreach (var file in Git("ls-tree","-r","--name-only",rev,"Assets/AIPlayerSpike/Scripts").Split('\n',StringSplitOptions.RemoveEmptyEntries).Where(x=>x.EndsWith(".cs"))) {
        var root = CSharpSyntaxTree.ParseText(Git("show",rev+":"+file),new CSharpParseOptions(LanguageVersion.CSharp8)).GetCompilationUnitRoot();
        if (root.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error)) throw new Exception("parse "+file);
        foreach (var n in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()) {
            string id=Id(n); if(!result.ContainsKey(id)) result[id]=new(); result[id].Add((file,n));
        }
    }
    return result;
}
if(args.Contains("--r-descriptions")) {
    int count=0, bad=0;
    foreach(var file in new[]{"AIBase.cs","AIBrain.Prep.cs","AICook.cs","AIFarm.cs","AIPlaces.cs"}) {
        var old=CSharpSyntaxTree.ParseText(Git("show","3c31536:Assets/AIPlayerSpike/Scripts/"+file)).GetRoot();
        var now=CSharpSyntaxTree.ParseText(Git("show","a760da1:Assets/AIPlayerSpike/Scripts/"+file)).GetRoot();
        var descs=old.DescendantNodes().OfType<AssignmentExpressionSyntax>().Where(x=>x.Left.ToString()=="desc").Select(x=>T(x.Right)).ToList();
        foreach(var call in now.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x=>x.Expression.ToString()=="AIApproach.WalkCandidate")) {
            count++; string desc=T(call.ArgumentList.Arguments.Last().Expression);
            if(!descs.Remove(desc)) {bad++; Console.WriteLine("description mismatch "+file);}
        }
    }
    Console.WriteLine($"R walk candidate descriptions: {count}, exact original expression token matches {count-bad}");
    Environment.ExitCode=bad;return;
}
if(args.Contains("--live-down")) {
    var before=Read("a07166f");var after=Read("902721e");int bad=0;
    foreach(var name in new[]{"AITileReader","AIWallData","MineSkill","TunnelPlan","AITunnel","TunnelSkill","AIVitals"}) {
        bool same=T(before[name].Single().node)==T(after[name].Single().node);
        Console.WriteLine(name+" whole declaration token equality="+same);if(!same)bad++;
    }
    var ot=(TypeDeclarationSyntax)before["TunnelSkill"].Single().node;var nt=(TypeDeclarationSyntax)after["TunnelSkill"].Single().node;
    bool wallFactOnly=T(ot).Replace("AIEdge . EdgeWallNote (","AIEdge . WallFact (")==T(nt);
    Console.WriteLine("TunnelSkill equality after the reviewed EdgeWallNote->WallFact rename="+wallFactOnly);if(wallFactOnly)bad--;
    foreach(var m in ot.Members.OfType<MethodDeclarationSyntax>()) {
        var n=nt.Members.OfType<MethodDeclarationSyntax>().Single(x=>x.Identifier.Text==m.Identifier.Text);
        if(T(m)!=T(n)) {
            var l=m.DescendantTokens().Select(x=>x.Text).ToArray();var r=n.DescendantTokens().Select(x=>x.Text).ToArray();int i=0;while(i<Math.Min(l.Length,r.Length)&&l[i]==r[i])i++;
            Console.WriteLine("TunnelSkill changed method="+m.Identifier.Text+" OLD "+string.Join(" ",l.Skip(Math.Max(0,i-8)).Take(65))+" NEW "+string.Join(" ",r.Skip(Math.Max(0,i-8)).Take(65)));
        }
    }
    foreach(var name in new[]{"Tick","StartSkill","EndSkill","Stop"}) {
        string[] Methods(Dictionary<string,List<(string file,BaseTypeDeclarationSyntax node)>> all) => all["AIBody"].SelectMany(x=>((TypeDeclarationSyntax)x.node).Members).OfType<MethodDeclarationSyntax>().Where(x=>x.Identifier.Text==name).Select(T).OrderBy(x=>x).ToArray();
        var left=Methods(before);var right=Methods(after);bool same=left.Length>0&&left.SequenceEqual(right);
        Console.WriteLine("AIBody."+name+" overloads="+left.Length+" token equality="+same);if(!same)bad++;
    }
    Environment.ExitCode=bad;return;
}
var a=Read("f512924"); var b=Read("fbb98b7"); int failures=0;
void Check(bool pass,string label) { if(!pass) { Console.WriteLine("DIFFERENT "+label); failures++; } }
Check(a.Keys.OrderBy(x=>x).SequenceEqual(b.Keys.OrderBy(x=>x)),"type identity set");
foreach(var (id,old) in a) {
    if(!b.TryGetValue(id,out var now)) continue;
    IEnumerable<string> Header(List<(string file,BaseTypeDeclarationSyntax node)> xs) => xs.Select(x=>x.node).Select(n=>string.Join(" ",n.Modifiers.Where(t=>!t.IsKind(SyntaxKind.PartialKeyword)).Select(t=>t.Text))+" "+n.Kind()+" "+n.Identifier.Text).Distinct().OrderBy(x=>x);
    IEnumerable<string> Attr(List<(string file,BaseTypeDeclarationSyntax node)> xs) => xs.SelectMany(x=>x.node.AttributeLists).Select(T).OrderBy(x=>x);
    IEnumerable<string> Bases(List<(string file,BaseTypeDeclarationSyntax node)> xs) => xs.Where(x=>x.node.BaseList!=null).Select(x=>T(x.node.BaseList)).OrderBy(x=>x);
    IEnumerable<MemberDeclarationSyntax> Members(List<(string file,BaseTypeDeclarationSyntax node)> xs) => xs.SelectMany(x=>x.node is TypeDeclarationSyntax t?t.Members:Enumerable.Empty<MemberDeclarationSyntax>());
    Check(Header(old).SequenceEqual(Header(now)),id+" declaration kind/modifiers");
    Check(Attr(old).SequenceEqual(Attr(now)),id+" attributes"); Check(Bases(old).SequenceEqual(Bases(now)),id+" base types");
    Check(Members(old).Select(T).OrderBy(x=>x).SequenceEqual(Members(now).Select(T).OrderBy(x=>x)),id+" member bodies/signatures/attributes within owner");
    bool Init(MemberDeclarationSyntax m) => m is FieldDeclarationSyntax f && !f.Modifiers.Any(t=>t.IsKind(SyntaxKind.ConstKeyword)) && f.Declaration.Variables.Any(v=>v.Initializer!=null) || m is PropertyDeclarationSyntax p && p.Initializer!=null;
    Check(Members(old).Where(Init).Select(T).SequenceEqual(Members(now).Where(Init).Select(T)),id+" initialized field/property order");
    if(old[0].node is EnumDeclarationSyntax) Check(T(old[0].node)==T(now[0].node),id+" enum full declaration");
}
Console.WriteLine($"Compared {a.Count} fully qualified top-level/nested type identities; failures {failures}");
Environment.ExitCode=failures;
