namespace LocationData
{
    public static class Locations
    {
        public static readonly Dictionary<string, Vector3> Location = new Dictionary<string, Vector3>()
        {
            {"SustanciaRoad", new Vector3(2051.47f, -887.94f, 79.14f) },
            {"PowerStation", new Vector3(2730.65f, 1382.87f, 24.12f) },
        };

        public static void AddLocation(string name, Vector3 coordinates)
        {
            Location.Add(name, coordinates);
        }
    }
}