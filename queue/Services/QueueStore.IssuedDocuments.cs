using System.Security.Cryptography;
using SmartQueueSystemGUI.Models;
namespace SmartQueueSystemGUI.Services;
public partial class QueueStore {
 public List<IssuedDocumentSummary> IssuedDocuments(string number,int actor,bool staff){lock(_lock){if(!_data.Tickets.Any(t=>t.TicketNumber==number&&(staff||t.CustomerUserId==actor)))return new();return _data.IssuedDocuments.Where(d=>d.TicketNumber==number).OrderByDescending(d=>d.IssuedAt).Select(d=>new IssuedDocumentSummary(d.Id,d.Code,d.Title,d.FileName,d.Sha256,d.IssuedAt,d.RevokedAt)).ToList();}}
 public (bool ok,string message) RegisterIssuedDocument(string number,int staff,string name,byte[] bytes){lock(_lock){
  if(DateTimeOffset.UtcNow<_reminderPauseUntil)return(false,"A restore is in progress. Try again shortly.");
  if(!_data.Users.Any(u=>u.Id==staff&&u.Role is "Admin" or "Staff"))return(false,"Office access is required.");
  var ticket=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==number&&t.ServiceType=="Document Request"&&t.DocumentStatus is "Ready for pickup" or "Released");
  if(ticket==null)return(false,"Only ready or released document requests can register an issued PDF.");
  if(_data.IssuedDocuments.Any(d=>d.TicketNumber==number&&!d.RevokedAt.HasValue))return(false,"An active issued document already exists. Revoke it before registering a replacement.");
  name=SafeFileName(name);if(name.Length is <5 or >120||SubmissionType(name,bytes)!="application/pdf")return(false,"Choose a PDF of up to 2 MB.");
  if(_data.IssuedDocuments.Count(d=>d.TicketNumber==number)>=5)return(false,"The five-document history limit has been reached.");
  if(_data.RequirementSubmissions.Sum(s=>(long)s.Length)+_data.IssuedDocuments.Sum(d=>(long)d.Length)+bytes.Length>MaxStoredSubmissionBytes||_data.RequirementSubmissions.Count+_data.IssuedDocuments.Count>=500)return(false,"Demo upload storage is full.");
  var before=Clone(_data);try{_data.IssuedDocuments.Add(new(){Id=Guid.NewGuid().ToString("N"),Code=Guid.NewGuid().ToString("N"),TicketNumber=number,Title=ticket.RequestType,Issuer=_data.Settings.EstablishmentName,FileName=name,ContentBase64=Convert.ToBase64String(bytes),Sha256=Convert.ToHexString(SHA256.HashData(bytes)),Length=bytes.Length,IssuedAt=CampusNow,IssuedBy=staff});AddLog("Issued document registered",$"Staff #{staff}: {number}.");Save();}catch{_data=before;throw;}
  return(true,"Issued PDF registered. Its verification QR identifies this exact file fingerprint.");
 }}
 public (bool ok,string message) RevokeIssuedDocument(string id,int staff,string? note){lock(_lock){
  if(!_data.Users.Any(u=>u.Id==staff&&u.Role is "Admin" or "Staff"))return(false,"Office access is required.");
  if(DateTimeOffset.UtcNow<_reminderPauseUntil)return(false,"A restore is in progress.");var doc=_data.IssuedDocuments.FirstOrDefault(d=>d.Id==id&&!d.RevokedAt.HasValue);if(doc==null)return(false,"Active issued document not found.");note=(note??"").Trim();if(note.Length is <5 or >600)return(false,"Give a revocation reason of 5–600 characters.");
  var before=Clone(_data);try{doc.RevokedAt=CampusNow;doc.RevocationNote=note;AddLog("Issued document revoked",$"Staff #{staff}: {doc.TicketNumber}, document {doc.Id}.");Save();}catch{_data=before;throw;}return(true,"Verification now shows this document as revoked.");
 }}
 public DocumentVerification? VerifyIssuedDocument(string code){lock(_lock){var d=_data.IssuedDocuments.FirstOrDefault(d=>d.Code==code);return d==null?null:new(d.Code,d.Title,d.Issuer,d.Sha256,d.IssuedAt,d.RevokedAt);}}
 public (byte[] bytes,string name)? DownloadIssuedDocument(string id,int actor,bool staff){lock(_lock){var d=_data.IssuedDocuments.FirstOrDefault(d=>d.Id==id);if(d==null||!_data.Tickets.Any(t=>t.TicketNumber==d.TicketNumber&&(staff||t.CustomerUserId==actor)))return null;var bytes=Convert.FromBase64String(d.ContentBase64);if(Convert.ToHexString(SHA256.HashData(bytes))!=d.Sha256)throw new InvalidDataException("Issued document failed its integrity check.");return(bytes,d.FileName);}}
 private static void ValidateIssuedDocuments(SmartQueueData data){
  if(data.IssuedDocuments==null||data.IssuedDocuments.Any(d=>d==null)||data.IssuedDocuments.Count+data.RequirementSubmissions.Count>500)throw new InvalidDataException("Invalid issued documents.");
  if(data.IssuedDocuments.Select(d=>d.Id).Distinct().Count()!=data.IssuedDocuments.Count||data.IssuedDocuments.Select(d=>d.Code).Distinct().Count()!=data.IssuedDocuments.Count)throw new InvalidDataException("Duplicate issued document records.");
  long total=data.RequirementSubmissions.Sum(s=>(long)s.Length);
  foreach(var d in data.IssuedDocuments){
   if(!Guid.TryParseExact(d.Id,"N",out _)||!Guid.TryParseExact(d.Code,"N",out _)||!data.Tickets.Any(t=>t.TicketNumber==d.TicketNumber&&t.ServiceType=="Document Request"&&t.RequestType==d.Title)||string.IsNullOrWhiteSpace(d.Issuer)||d.Issuer.Length>200||d.FileName==null||d.FileName.Length is <5 or >120||d.FileName!=SafeFileName(d.FileName)||d.ContentBase64==null||d.ContentBase64.Length>((MaxSubmissionBytes+2)/3)*4||d.IssuedAt==default||d.RevokedAt<d.IssuedAt||d.RevocationNote==null||d.RevocationNote.Length>600||d.RevokedAt.HasValue&&d.RevocationNote.Length<5)throw new InvalidDataException("Invalid issued document metadata.");
   byte[] bytes;try{bytes=Convert.FromBase64String(d.ContentBase64);}catch(FormatException){throw new InvalidDataException("Invalid issued PDF encoding.");}
   if(bytes.Length!=d.Length||SubmissionType(d.FileName,bytes)!="application/pdf"||Convert.ToHexString(SHA256.HashData(bytes))!=d.Sha256||(total+=bytes.Length)>MaxStoredSubmissionBytes)throw new InvalidDataException("Invalid issued PDF or storage quota exceeded.");
  }
  if(data.IssuedDocuments.GroupBy(d=>d.TicketNumber).Any(g=>g.Count()>5||g.Count(d=>!d.RevokedAt.HasValue)>1))throw new InvalidDataException("Invalid issued document history.");
 }
}
