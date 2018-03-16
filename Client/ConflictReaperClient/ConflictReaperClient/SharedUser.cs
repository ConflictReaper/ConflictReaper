using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    public class SharedUser : IEquatable<SharedUser>
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public bool   StatusFlag { get; set; }
        public string Status { get; set; }
        public string Background { get; set; }

        public SharedUser() { }
        public SharedUser(string email, string name)
        {
            Email = email;
            Name = name;
            setStatus(false);
        }

        public void setStatus(bool value)
        {
            StatusFlag = value;
            if (value)
            {
                Status = "Online";
                Background = "Green";
            }
            else
            {
                Status = "Offline";
                Background = "LightGray";
            }
        }

        public bool Equals(SharedUser obj)
        {
            if (obj.Email.Equals(Email) && obj.Name.Equals(Name))
                return true;
            else
                return false;
        }

        public override int GetHashCode()
        {
            return Email.GetHashCode();
        }
    }
}
