using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using apbd_zad8.Models;

namespace apbd_zad8.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly IConfiguration _cfg;

    public ClientsController(IConfiguration _cfg)
    {
        this._cfg = _cfg;
    }

    [HttpPost]
    public IActionResult AddClient([FromBody] ClientDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Telephone) ||
            string.IsNullOrWhiteSpace(dto.Pesel))
        {
            return BadRequest("Wszystkie pola są wymagane");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return BadRequest("Błąd formatu email");
        }

        int createIdx;

        using (var connection = new SqlConnection(_cfg.GetConnectionString("DefaultConnection")))
        {
            connection.Open();

            var command = new SqlCommand(
                @"INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                  OUTPUT INSERTED.IdClient
                  VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)", connection);

            command.Parameters.AddWithValue("@FirstName", dto.FirstName);
            command.Parameters.AddWithValue("@LastName", dto.LastName);
            command.Parameters.AddWithValue("@Email", dto.Email);
            command.Parameters.AddWithValue("@Telephone", dto.Telephone);
            command.Parameters.AddWithValue("@Pesel", dto.Pesel);

            try
            {
                createIdx = (int)command.ExecuteScalar();
            }
            catch (SqlException e)
            {
                return StatusCode(500, $"Błąd bazy : {e.Message}");
            }
        }

        return Created($"/api/clients/{createIdx}", new { IdClient = createIdx });
    }

    [HttpPut("{id}/trips/{tripId}")]
    public IActionResult RegisterClientToTrip(int id, int tripId)
    {
        using (var con = new SqlConnection(_cfg.GetConnectionString("DefaultConnection")))
        {
            con.Open();

            var checkClientCmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @Id", con);
            checkClientCmd.Parameters.AddWithValue("@Id", id);
            if (checkClientCmd.ExecuteScalar() == null)
            {
                return NotFound($"Nie ma klienta o {id} ID!!!");
            }

            var checkTripCmd = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @TripId", con);
            checkTripCmd.Parameters.AddWithValue("@TripId", tripId);
            object? maxPeopleObj = checkTripCmd.ExecuteScalar();
            if (maxPeopleObj == null)
            {
                return NotFound($"Wycieczka {tripId} ID nie istnieje!!!");
            }
            
            var isCmd = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @Id AND IdTrip = @TripId",
                con);
            isCmd.Parameters.AddWithValue("@Id", id);
            isCmd.Parameters.AddWithValue("@TripId", tripId);
            if (isCmd.ExecuteScalar() != null)
            {
                return Conflict("Klient już jest na tej wycieczce!!!");
            }
            
            
            int maxPeople = (int)maxPeopleObj;

            var countCmd = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @TripId", con);
            countCmd.Parameters.AddWithValue("@TripId", tripId);
            int count = (int)countCmd.ExecuteScalar();

            if (count >= maxPeople)
            {
                return BadRequest("Wycieczka osiągneła max liczebności!!!");
            }


            var inCmd = new SqlCommand(
                @"INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
              VALUES (@Id, @TripId, @Now)", con);
            inCmd.Parameters.AddWithValue("@Id", id);
            inCmd.Parameters.AddWithValue("@TripId", tripId);
            
            int dateInt = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            inCmd.Parameters.AddWithValue("@Now", dateInt);

            inCmd.ExecuteNonQuery();
        }

        return Ok("Kient został zapisany na wycieczke!!!");
    }

    [HttpDelete("{id}/trips/{tripId}")]
    public IActionResult UnregisterClientFromTrip(int id, int tripId)
    {
        using var con = new SqlConnection(_cfg.GetConnectionString("DefaultConnection"));
        
        con.Open();

        var checkCmd = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @Id AND IdTrip = @TripId",
            con);
        checkCmd.Parameters.AddWithValue("@Id", id);
        checkCmd.Parameters.AddWithValue("@TripId", tripId);

        if (checkCmd.ExecuteScalar() == null)
        {
            return NotFound("Klient nie można usunąć bo nie jest zzapisany!!!");
        }

        using (var del = new SqlCommand("DELETE FROM Client_Trip WHERE IdClient=@c AND IdTrip=@t", con))
        {
            del.Parameters.AddWithValue("@c", id);
            del.Parameters.AddWithValue("@t", tripId);
            del.ExecuteNonQuery();                               
        }

        return Ok("Klient został wypisany z wycieczki!!!");
    }
    
    [HttpGet("{id}/trips")] 
    public IActionResult GetClientTrips(int id) 
        {
            using var con = new SqlConnection(_cfg.GetConnectionString("DefaultConnection"));
            con.Open();

            using (var chkCli = new SqlCommand("SELECT 1 FROM Client WHERE IdClient=@id", con))
            {
                chkCli.Parameters.AddWithValue("@id", id);
                if (chkCli.ExecuteScalar() == null)
                {
                    return NotFound("Nie ma takiego klienta!!!");
                }
            }

            var sql = @"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                       ct.RegisteredAt, c.Name AS CountryName
                FROM Client_Trip ct
                JOIN Trip t ON t.IdTrip = ct.IdTrip
                JOIN Country_Trip ctr ON ctr.IdTrip = t.IdTrip
                JOIN Country c ON ctr.IdCountry = c.IdCountry
                WHERE ct.IdClient = @id";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", id);

            var map = new Dictionary<int, 
                (int Id, string Name, string Desc, string From, string To, int Max, string RegAt, List<string> Cnts)>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                int idx = (int)rdr["IdTrip"];
                if (!map.ContainsKey(idx))
                {
                    map[idx] = (
                        Id: idx,
                        Name: rdr["Name"].ToString()!,
                        Desc: rdr["Description"].ToString()!,
                        From: ((DateTime)rdr["DateFrom"]).ToString("yyyy-MM-dd"),
                        To: ((DateTime)rdr["DateTo"]).ToString("yyyy-MM-dd"),
                        Max: (int)rdr["MaxPeople"],
                        RegAt: rdr["RegisteredAt"].ToString()!,
                        Cnts: new List<string>());
                }
                map[idx].Cnts.Add(rdr["CountryName"].ToString()!);
            }

            var result = new List<object>();                        
            foreach (var entry in map.Values)
            {
                result.Add(new {
                    entry.Id,
                    entry.Name,
                    entry.Desc,
                    entry.From,
                    entry.To,
                    entry.Max,
                    entry.RegAt,
                    Countries = entry.Cnts
                });
            }
        return Ok(result);
    }
}