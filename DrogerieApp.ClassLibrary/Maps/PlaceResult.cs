namespace DrogerieApp.ClassLibrary.Maps;

public class PlaceResult
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public PhotoAttribution Photo { get; set; } = new();
    public Location Location { get; set; } = new();
}


public class Location
{
    public double lat { get; set; } = double.MinValue;
    public double lng { get; set; } = double.MinValue;

    public override string ToString()
    {
        return $"{lat}; {lng}";
    }
}

public class PhotoAttribution
{
    public IEnumerable<Photo>? authorAttributions { get; set; }
}

public class Photo
{
    public string displayName { get; set; } = string.Empty;
    public string uri { get; set; } = string.Empty;
    public string photoURI { get; set; } = string.Empty;
}