namespace APBD_05.models;

public class ClientDTO
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Telephone { get; set; }
    public string Pesel { get; set; }

    public bool ValidateRequiredFields()
    {
        return this.FirstName != null && this.LastName != null && this.Email != null;
    }
}