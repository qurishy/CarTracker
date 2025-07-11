namespace MVS_Project.Services
{
    public class RealGpsService //: IGpsDataService
    {


        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public RealGpsService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        //public async Task<IEnumerable<CarPosition>> GetLatestPositionsAsync(string countryCode)
        //{
        //    var apiKey = _config["GpsApi:Key"];
        //    var response = await _httpClient.GetFromJsonAsync<GpsApiResponse>(
        //        $"https://api.gpsprovider.com/v1/vehicles?country={countryCode}&apiKey={apiKey}");

        //    return response.Vehicles.Select(v => new CarPosition(
        //        v.Id,
        //        v.Location.Latitude,
        //        v.Location.Longitude));
        //}
    }
}
