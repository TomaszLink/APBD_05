using APBD_05.models;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("Default");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.MapGet("/api/trips", () =>
{
    using var sqlConnection = new SqlConnection(connectionString);
    sqlConnection.Open();
    var selectQuery = "SELECT * FROM Trip";
    using var command = new SqlCommand(selectQuery, sqlConnection);
    using var reader = command.ExecuteReader();
    List<Trip> trips = new List<Trip>();
    if (reader.HasRows)
    {
        while (reader.Read())
        {
            Trip trip = new Trip();
            trip.IdTrip = reader.GetInt32(0);
            trip.Name = reader.GetString(1);
            trip.Description = reader.GetString(2);
            trip.DateFrom = reader.GetDateTime(3);
            trip.DateTo = reader.GetDateTime(4);
            trip.MaxPeople = reader.GetInt32(5);
            trips.Add(trip);
        }
    }
    return trips;
});


app.MapGet("api/clients/{id}/trips", (string id) =>
{
    using var sqlConnection = new SqlConnection(connectionString);
    sqlConnection.Open();
    
    var selectQuery_ = "SELECT * FROM Client WHERE IdClient=@id";
    using var command_ = new SqlCommand(selectQuery_, sqlConnection);
    command_.Parameters.AddWithValue("@id", id);
    using var reader_ = command_.ExecuteReader();
    if(!reader_.HasRows)
        return Results.NotFound("Client with this id doesn't exist");
    
    sqlConnection.Close();
    sqlConnection.Open();

    var selectQuery = @"
        SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople
        FROM Client_Trip ct
        INNER JOIN Trip t ON ct.IdTrip = t.IdTrip
        WHERE ct.IdClient = @id";
    
    using var command = new SqlCommand(selectQuery, sqlConnection);
    command.Parameters.AddWithValue("@id", id);

    List<Trip> trips = new List<Trip>();

    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        trips.Add(new Trip
        {
            IdTrip = reader.GetInt32(0),
            Name = reader.GetString(1),
            Description = reader.GetString(2),
            DateFrom = reader.GetDateTime(3),
            DateTo = reader.GetDateTime(4),
            MaxPeople = reader.GetInt32(5)
        });
    }
    return Results.Ok(trips);
});


app.MapPost("/api/clients", (ClientDTO clientDTO) =>
{
    if(!clientDTO.ValidateRequiredFields())
        return Results.BadRequest("Fields firstname, lastname and email cannot be empty");
    
    using var sqlConnection = new SqlConnection(connectionString);
    sqlConnection.Open();
    var insertCommand =
        "INSERT INTO Client(FirstName, LastName, Email, Telephone, Pesel) VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)";
    using var command = new SqlCommand(insertCommand, sqlConnection);
    command.Parameters.AddWithValue("@FirstName", clientDTO.FirstName);
    command.Parameters.AddWithValue("@LastName", clientDTO.LastName);
    command.Parameters.AddWithValue("@Email", clientDTO.Email);
    command.Parameters.AddWithValue("@Telephone", clientDTO.Telephone); 
    command.Parameters.AddWithValue("@Pesel", clientDTO.Pesel);
    command.ExecuteNonQuery();
    return Results.Created();
});

app.MapPut("/api/clients/{id}/trips/{tripId}", (int id, int tripId) =>
{
    using var sqlConnection = new SqlConnection(connectionString);
    bool clientExists = CheckClientExists(id, sqlConnection);
    if (!clientExists)
        return Results.NotFound("Client with this id doesn't exist");

    sqlConnection.Close();

    Trip trip = FindTrip(tripId, sqlConnection);
    if(trip == null)
    {
        return Results.NotFound("Trip with this id doesn't exist");
    }
    
    sqlConnection.Open();
    var selectQuery = "SELECT COUNT(*) FROM Client_Trip WHERE IdTrip=@id";
    using var commandClientsTrips = new SqlCommand(selectQuery, sqlConnection);
    commandClientsTrips.Parameters.AddWithValue("@id", tripId);
    using var readerClientsTrips = commandClientsTrips.ExecuteReader();
    
    int countClientsTrips = -1;
    
    if (readerClientsTrips.Read())
    {
        countClientsTrips = readerClientsTrips.GetInt32(0);
    }

    Console.WriteLine(countClientsTrips);
    if(countClientsTrips >= trip.MaxPeople)
        return Results.Conflict("There are too many people in this trip");

    sqlConnection.Close();
    var clientTripExists = CheckClientTripExists(id, tripId, sqlConnection);
    
    if(clientTripExists)
        return Results.Conflict("Client-Trip already exists");

    sqlConnection.Open();
    
    DateTime date = DateTime.Now;
    int formattedDate = int.Parse(date.ToString("yyyyMMdd"));
    
    var insertCommandSQL =
        "INSERT INTO Client_Trip(IdClient, IdTrip, RegisteredAt) VALUES (@IdClient, @IdTrip, @RegisteredAt)";
    using var insertCommand = new SqlCommand(insertCommandSQL, sqlConnection);
    insertCommand.Parameters.AddWithValue("@IdClient", id);
    insertCommand.Parameters.AddWithValue("@IdTrip", tripId);
    insertCommand.Parameters.AddWithValue("@RegisteredAt", formattedDate);
    insertCommand.ExecuteNonQuery();

    return Results.Accepted();
});


app.MapDelete("/api/clients/{id}/trips/{tripId}", (int id, int tripId) =>
{
    using var sqlConnection = new SqlConnection(connectionString);
    var clientTripExists = CheckClientTripExists(id, tripId, sqlConnection);
    if(!clientTripExists)
        return Results.NotFound("Client-Trip doesn't exists");

    sqlConnection.Open();
    
    var deleteCommandSQL =
        "DELETE FROM Client_Trip WHERE IdClient=@IdClient AND IdTrip=@IdTrip";
    using var insertCommand = new SqlCommand(deleteCommandSQL, sqlConnection);
    insertCommand.Parameters.AddWithValue("@IdClient", id);
    insertCommand.Parameters.AddWithValue("@IdTrip", tripId);
    insertCommand.ExecuteNonQuery();

    return Results.Accepted();
});


static bool CheckClientTripExists(int clientId, int tripId, SqlConnection sqlConnection)
{
    sqlConnection.Open();
    var selectQuery= "SELECT * FROM Client_Trip WHERE IdClient=@clientId AND IdTrip=@tripId";
    using var command = new SqlCommand(selectQuery, sqlConnection);
    command.Parameters.AddWithValue("@clientId", clientId);
    command.Parameters.AddWithValue("@tripId", tripId);
    using var reader = command.ExecuteReader();
    bool result = reader.Read();
    sqlConnection.Close();
    return result;
}


static Trip FindTrip(int tripId, SqlConnection sqlConnection)
{
    sqlConnection.Open();
    
    var selectQuery = "SELECT * FROM Trip WHERE IdTrip=@id";
    using var commandTrip = new SqlCommand(selectQuery, sqlConnection);
    commandTrip.Parameters.AddWithValue("@id", tripId);
    using var readerTrip = commandTrip.ExecuteReader();
    Trip trip = null;
    if (readerTrip.Read())
    {
        trip = new Trip();
        trip.IdTrip = readerTrip.GetInt32(0);
        trip.MaxPeople = readerTrip.GetInt32(5);
    }
    sqlConnection.Close();
    return trip;
}


static bool CheckClientExists(int clientId, SqlConnection sqlConnection)
{
    sqlConnection.Open();
    var selectQuery = "SELECT * FROM Client WHERE IdClient=@id";
    using var command = new SqlCommand(selectQuery, sqlConnection);
    command.Parameters.AddWithValue("@id", clientId);
    using var reader = command.ExecuteReader();
    bool result = reader.Read();
    sqlConnection.Close();
    return result;
}

app.Run();