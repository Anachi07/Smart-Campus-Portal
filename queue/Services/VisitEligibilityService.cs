using System.Net.Http.Json;
using SmartQueueSystemGUI.Models;
namespace SmartQueueSystemGUI.Services;

public class VisitEligibilityService
{
    private readonly IHttpClientFactory _clients;
    public VisitEligibilityService(IHttpClientFactory clients) => _clients=clients;
    public async Task<Dictionary<int,VisitEligibility>> Fetch(IEnumerable<int> campusIds,CancellationToken token)
    {
        var ids=campusIds.Distinct().ToList();
        if(ids.Count==0 || ids.Count>20) throw new HttpRequestException("Unable to verify visit eligibility.");
        using var request=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:8082/assistant-schedules.php") {Content=JsonContent.Create(ids)};
        request.Headers.Add("X-Campus-Secret",Environment.GetEnvironmentVariable("CAMPUS_INTERNAL_SECRET"));
        using var response=await _clients.CreateClient("campus").SendAsync(request,token);
        response.EnsureSuccessStatusCode();
        var values=await response.Content.ReadFromJsonAsync<List<VisitEligibility>>(cancellationToken:token)??new();
        if(ids.Any(id=>!values.Any(v=>v.CampusId==id)))throw new HttpRequestException("Unable to verify all participants.");
        return values.ToDictionary(v=>v.CampusId);
    }
}
