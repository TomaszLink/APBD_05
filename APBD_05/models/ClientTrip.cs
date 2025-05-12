namespace APBD_05.models;

public class ClientTrip
{
    public int IdClient { get; set; }
    public string IdTrip { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime PaymentDate { get; set; }
}