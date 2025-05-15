using apbd_zad8.Models;

namespace apbd_zad8.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly string _conStr;
    public TripsController(IConfiguration cfg)
    {
        _conStr = cfg.GetConnectionString("DefaultConnection");
    }

    [HttpGet]
    public IActionResult GetTrips()
    {
        var list = new List<TripDto>();
        const string query = @"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                       c.Name AS CountryName
                FROM Trip t
                LEFT JOIN Country_Trip ct ON ct.IdTrip = t.IdTrip
                LEFT JOIN Country c ON c.IdCountry = ct.IdCountry";

        using (var conn = new SqlConnection(_conStr))
        using (var cmd  = new SqlCommand(query, conn))
        {
            conn.Open();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                int id = (int)rdr["IdTrip"];                  
                var existing = list.Find(x => x.Id == id);
                if (existing == null)
                {
                    existing = new TripDto
                    {
                        Id = id,
                        Name = rdr["Name"].ToString(),
                        Description = rdr["Description"].ToString(),
                        DateFrom = (DateTime)rdr["DateFrom"],
                        DateTo = (DateTime)rdr["DateTo"],
                        MaxPeople = (int)rdr["MaxPeople"],
                        Countries = new List<string>()
                    };
                    list.Add(existing);
                }
                if (rdr["CountryName"] != DBNull.Value)
                    existing.Countries.Add(rdr["CountryName"].ToString()!);
            }
        }
        return Ok(list);
    }
}