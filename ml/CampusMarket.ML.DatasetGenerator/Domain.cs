namespace CampusMarket.ML.DatasetGenerator;

// Mirrors CampusMarket.Api.Domain.Listings.Category and Condition.
// Kept as a local copy so this dev tool stays decoupled from the API project.
public enum Category { Textbook, Notes, Electronics, LabEquipment, Other }

public enum Condition { New, Good, Worn }
