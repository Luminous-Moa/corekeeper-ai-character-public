using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using System.Text.Encodings.Web;

var root = Path.GetFullPath(args[0]);
var paths = Directory.GetFiles(Path.Combine(root,"Assets"),"*.cs",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
var trees = paths.Select(p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),new CSharpParseOptions(LanguageVersion.Latest),Path.GetRelativePath(root,p).Replace('\\','/'))).ToArray();
var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator).Select(p=>MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("GraphSource",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var nodes = new List<object>(); var edges = new List<object>(); var facts = new List<object>(); var diagnostics = new List<object>();
var ids = new Dictionary<SyntaxNode,string>(); var syms = new Dictionary<ISymbol,string>(SymbolEqualityComparer.Default);
int Line(SyntaxNode n)=>n.SyntaxTree.GetLineSpan(n.Span).StartLinePosition.Line+1;
int End(SyntaxNode n)=>n.SyntaxTree.GetLineSpan(n.Span).EndLinePosition.Line+1;
bool Decl(SyntaxNode n)=>n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or PropertyDeclarationSyntax or EventDeclarationSyntax or AccessorDeclarationSyntax or AnonymousFunctionExpressionSyntax || n is VariableDeclaratorSyntax v && v.Parent?.Parent is BaseFieldDeclarationSyntax;
string Name(SyntaxNode n)=>n switch {BaseTypeDeclarationSyntax x=>x.Identifier.Text,DelegateDeclarationSyntax x=>x.Identifier.Text,MethodDeclarationSyntax x=>x.Identifier.Text,ConstructorDeclarationSyntax x=>x.Identifier.Text,LocalFunctionStatementSyntax x=>x.Identifier.Text,PropertyDeclarationSyntax x=>x.Identifier.Text,EventDeclarationSyntax x=>x.Identifier.Text,VariableDeclaratorSyntax x=>x.Identifier.Text,AccessorDeclarationSyntax x=>x.Keyword.Text,_=>n.Kind().ToString()};
string Owner(SyntaxNode n)=>n.AncestorsAndSelf().Where(ids.ContainsKey).Select(x=>ids[x]).FirstOrDefault()??"code:"+n.SyntaxTree.FilePath;
foreach(var tree in trees){
 var model=compilation.GetSemanticModel(tree); var r=tree.GetRoot();
 nodes.Add(new{id="code:"+tree.FilePath,type="코드",kind="file",file=tree.FilePath,lineStart=1,lineEnd=End(r),name=Path.GetFileName(tree.FilePath)});
 foreach(var n in r.DescendantNodes().Where(Decl)){
  var sym=n is AnonymousFunctionExpressionSyntax ? null : model.GetDeclaredSymbol(n); var signature=sym?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)??Name(n)+"@"+n.SpanStart;
  var id="code:"+tree.FilePath+"#"+signature;
  if(ids.Values.Contains(id)) id+="@"+n.SpanStart;
  ids[n]=id; if(sym!=null)syms[sym.OriginalDefinition]=id;
  nodes.Add(new{id,type="코드",kind=n.Kind().ToString(),file=tree.FilePath,lineStart=Line(n),lineEnd=End(n),start=n.SpanStart,end=n.Span.End,name=Name(n),signature,modifiers=n is MemberDeclarationSyntax md?string.Join(" ",md.Modifiers.Select(t=>t.Text)):null,attributes=n.ChildNodes().OfType<AttributeListSyntax>().Select(x=>x.ToString()).ToArray(),bases=n is BaseTypeDeclarationSyntax bt?bt.BaseList?.ToString():null});
 }
 foreach(var d in tree.GetDiagnostics()) diagnostics.Add(new {file=tree.FilePath,kind="syntax",text=d.ToString()});
}
string? Resolve(ISymbol? s)=>s!=null && syms.TryGetValue(s.OriginalDefinition,out var id)?id:null;
foreach(var tree in trees){
 var model=compilation.GetSemanticModel(tree); var r=tree.GetRoot();
 foreach(var n in r.DescendantNodes().Where(Decl)){
  edges.Add(new{from=Owner(n.Parent!),to=ids[n],type="포함"});
  if(n is AnonymousFunctionExpressionSyntax) {edges.Add(new{from=Owner(n.Parent!),to=ids[n],type="reference",kind="defines",file=tree.FilePath,line=Line(n)}); if(n.Ancestors().TakeWhile(x=>!Decl(x)).Any(x=>x is ArgumentSyntax or EqualsValueClauseSyntax or AssignmentExpressionSyntax)) edges.Add(new{from=Owner(n.Parent!),to=ids[n],type="reference",kind="callback",file=tree.FilePath,line=Line(n)});}
  if(n is AccessorDeclarationSyntax)edges.Add(new{from=Owner(n.Parent!),to=ids[n],type="reference",kind="framework-invoked",file=tree.FilePath,line=Line(n),resolution="property-accessor-potential"});
  if(n is VariableDeclaratorSyntax vd && vd.Initializer!=null && vd.Parent?.Parent is FieldDeclarationSyntax fd && fd.Modifiers.Any(t=>t.IsKind(SyntaxKind.StaticKeyword)))edges.Add(new{from=Owner(fd.Parent!),to=ids[n],type="reference",kind="framework-invoked",file=tree.FilePath,line=Line(n),resolution="static-field-initializer-potential",possible=true});
  if(n is MethodDeclarationSyntax && model.GetDeclaredSymbol(n) is IMethodSymbol ms && Resolve(ms.OverriddenMethod) is string baseId)edges.Add(new{from=baseId,to=ids[n],type="reference",kind="framework-invoked",file=tree.FilePath,line=Line(n),resolution="virtual-override-potential",possible=true});
 }
 foreach(var n in r.DescendantNodes()){
  var owner=Owner(n);
  if(n is InvocationExpressionSyntax call){
   var info=model.GetSymbolInfo(call); var target=Resolve(info.Symbol); var candidates=info.CandidateSymbols.Select(Resolve).Where(x=>x!=null).Distinct().ToArray();
   if(target!=null)edges.Add(new{from=owner,to=target,type="reference",kind="call",file=tree.FilePath,line=Line(n),resolution="semantic"});
   if(info.Symbol is IMethodSymbol im && im.MethodKind==MethodKind.DelegateInvoke && Resolve(model.GetSymbolInfo(call.Expression).Symbol) is string delegateId)edges.Add(new{from=owner,to=delegateId,type="reference",kind="callback",file=tree.FilePath,line=Line(n),resolution="delegate-field-invoke"});
   facts.Add(new {form="call",from=owner,file=tree.FilePath,line=Line(n),endLine=End(n),expr=call.Expression.ToString(),args=call.ArgumentList.Arguments.Select(a=>new {expr=a.Expression.ToString(),constant=model.GetConstantValue(a.Expression) is var c && c.HasValue ? c.Value : null}).ToArray(),target,candidates,reason=info.CandidateReason.ToString()});
  } else if(n is ObjectCreationExpressionSyntax oc){
   var info=model.GetSymbolInfo(oc); var target=Resolve(info.Symbol); if(target!=null)edges.Add(new{from=owner,to=target,type="reference",kind="call",file=tree.FilePath,line=Line(n),resolution="semantic"});
   facts.Add(new{form="new",from=owner,file=tree.FilePath,line=Line(n),expr=oc.Type.ToString(),args=oc.ArgumentList?.Arguments.Select(a=>new{expr=a.Expression.ToString(),constant=model.GetConstantValue(a.Expression) is var c && c.HasValue?c.Value:null}).ToArray(),target});
  } else if(n is IdentifierNameSyntax ident){
   var info=model.GetSymbolInfo(ident); var target=Resolve(info.Symbol);
   if(target!=null && target!=owner){
    var callback=info.Symbol is IMethodSymbol && !(n.Parent is InvocationExpressionSyntax || n.Parent is MemberAccessExpressionSyntax ma && ma.Parent is InvocationExpressionSyntax);
    if(callback)edges.Add(new{from=owner,to=target,type="reference",kind="callback",file=tree.FilePath,line=Line(n),resolution="semantic-method-group"});
    else if(info.Symbol is ITypeSymbol)edges.Add(new{from=owner,to=target,type="reference",kind="type reference",file=tree.FilePath,line=Line(n),resolution="semantic"});
    else if(info.Symbol is IPropertySymbol)edges.Add(new{from=owner,to=target,type="reference",kind="call",file=tree.FilePath,line=Line(n),resolution="property-access"});
   }
  }
  if(n is AnonymousFunctionExpressionSyntax af)facts.Add(new{form="lambda",from=owner,file=tree.FilePath,line=Line(n),expr=af.ToString()});
  if(n is AttributeSyntax at)facts.Add(new{form="attribute",from=owner,file=tree.FilePath,line=Line(n),expr=at.ToString()});
  if(n is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))facts.Add(new{form="string",from=owner,file=tree.FilePath,line=Line(n),value=lit.Token.ValueText});
 }
}
foreach(var d in compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error))diagnostics.Add(new{file=d.Location.SourceTree?.FilePath,kind="semantic",line=d.Location.GetLineSpan().StartLinePosition.Line+1,code=d.Id,text=d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)});
File.WriteAllText(args[1],JsonSerializer.Serialize(new{parser="Microsoft.CodeAnalysis.CSharp",version=typeof(CSharpSyntaxTree).Assembly.GetName().Version?.ToString(),nodes,edges,facts,diagnostics=diagnostics.OrderBy(d=>JsonSerializer.Serialize(d),StringComparer.Ordinal)},new JsonSerializerOptions{WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals})+"\n");
