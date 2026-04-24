namespace SharedResources.Models
{
    public class UserAccessModel
    {
        public int IdUserAccess { get; set; }

        public int IdUser { get; set; }

        public UserModel? User { get; set; }

        public DateTime ExpirationDate { get; set; }

        public bool Type { get; set; }
    }
}
