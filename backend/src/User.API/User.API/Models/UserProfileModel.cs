
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using User.API.Models;

public class UserProfileModel : DocumentBase
{
    [BsonElement("FirstName")]
    public string FirstName { get; set; }
    
    [BsonElement("LastName")]
    public string LastName { get; set; }

    [BsonElement("Email")]
    public string Email { get; set; }

    [BsonElement("Phone")]
    public string Phone { get; set; }

    //[BsonElement("Geography")]
    //public string Geography { get; set; }

    [BsonElement("DOB")]
    public DateTime DOB { get; set; }

    [BsonElement("Social")]
    public Social Social { get; set; }

    [BsonElement("Interests")]
    public List<string> Interests { get; set; }

    [BsonElement("UserPhoto")]
    public string UserPhoto { get; set; }

    [BsonElement("KYCStatus")]
    public string KYCStatus { get; set; }

    [BsonElement("PhoneVerified")]
    public bool PhoneVerified { get; set; }

    [BsonElement("EmailVerified")]
    public bool EmailVerified { get; set; }

    [BsonElement("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("IsActive")]
    public bool IsActive { get; set; }
}

public class Social
{
    [BsonElement("FacebookId")]
    public string FacebookId { get; set; }

    [BsonElement("TwitterId")]
    public string TwitterId { get; set; }

    [BsonElement("InstagramId")]
    public string InstagramId { get; set; }

    [BsonElement("DiscordId")]
    public string DiscordId { get; set; }
}
