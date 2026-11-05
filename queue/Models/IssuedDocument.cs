namespace SmartQueueSystemGUI.Models;
public class IssuedDocument {
 public string Id{get;set;}="";public string Code{get;set;}="";public string TicketNumber{get;set;}="";
 public string Title{get;set;}="";public string Issuer{get;set;}="";public string FileName{get;set;}="";
 public string ContentBase64{get;set;}="";public string Sha256{get;set;}="";public int Length{get;set;}
 public DateTimeOffset IssuedAt{get;set;}public int IssuedBy{get;set;}public DateTimeOffset? RevokedAt{get;set;}public string RevocationNote{get;set;}="";
}
public record IssuedDocumentSummary(string Id,string Code,string Title,string FileName,string Sha256,DateTimeOffset IssuedAt,DateTimeOffset? RevokedAt);
public record DocumentVerification(string Code,string Title,string Issuer,string Sha256,DateTimeOffset IssuedAt,DateTimeOffset? RevokedAt);
