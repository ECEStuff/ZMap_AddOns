using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Drawing;
using Newtonsoft.Json;
using System.Diagnostics;

namespace X3_Mayhem_Galaxy_Generator
{
    // A Sector contains:
    // 1 Background
    // 1 Sun
    // 1 or more Planets
    // 1-4 Gates
    // 1 or more Asteroids
    // 1 or more Signs (specials)
   
    /// <summary>
    /// Universe encapsulates the structure of a universe as well as the ability to read/parse a x3_universe.xml file, and to write
    /// both the x3_universe.xml and the 00749.bod file, both of which are used by X3.
    /// The classes are similar enough in nature and composition that I stuffed them all in this one file for ease of editing and navigation.  Separate them if you are anal.
    /// </summary>
    public class X3Galaxy
    {
        static readonly X3Galaxy m_Instance = new X3Galaxy();
        public int MaxX, MaxY;
        public List<X3Sector> Sectors = new List<X3Sector>();
        private Random m_Rand = new Random();
        public GalaxySettings GalaxyCreationSettings = new GalaxySettings();
        public Dictionary<string, string> LocalizationDictionary = new Dictionary<string, string>();

        public static bool HasUnknownEnclave()
        {
            foreach (X3Sector sector in m_Instance.Sectors)
            {
                if ((ERace)sector.r == ERace.Unknown)
                {
                    if (sector.Gates.Count == 1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        private X3Galaxy() { }

        public static X3Galaxy Instance
        {
            get
            {
                return m_Instance;
            }
        }

        public void SetSize(int maxx, int maxy)
        {
            MaxX = maxx;
            MaxY = maxy;
        }

        public X3Galaxy(int maxx, int maxy)
        {
            MaxX = maxx;
            MaxY = maxy;
        }



        /// <summary>
        /// Systems have just been generated.
        /// Here, we are going through each Sector that was Generated and assigning it a Sector Name and an available Name/Sound combination.
        /// 
        /// </summary>
        /// <param name="masterSystemList"></param>
        public void AssignSystemNamesAndSoundToGeneratedSystems(List<SystemListItem> masterSystemList)
        {
            List<string> usedNames = new List<string>();

            foreach (X3Sector sector in X3Galaxy.Instance.Sectors)
            {
                string sectorx = (sector.x + 1).ToString("00");         // 01, 02 etc.
                string sectory = (sector.y + 1).ToString("00");
                string sectorId = "102" + sectory + sectorx;             // 1020101, 1020301 etc.
                sector.SectorID = sectorId;

                // Find the next available one from the master system list that we have not used yet.
                SystemListItem nextAvailable = masterSystemList.Find(item => !usedNames.Contains(item.Name));
                if (nextAvailable == null) throw new Exception("Couldn't find another available system name to use from the sector_names templates");

                // Store it all into the sector for ease of retrieval when creating the output files.
                sector.SectorName = nextAvailable.Name;
                sector.StreamID = nextAvailable.StreamID;
                sector.StreamLocation = nextAvailable.StreamLocation;
                sector.StreamLength = nextAvailable.StreamLength;

                usedNames.Add(sector.SectorName);
            }
        }



        public void CreateX3UniverseFile(string file)
        {
            List<string> contents = new List<string>();

            contents.Add("<?xml version = \"1.0\" encoding = \"UTF-8\" ?>");
            contents.Add("<universe>");
            foreach (X3Sector sector in Sectors)
            {
                sector.CreateXml(contents);
            }
            contents.Add("</universe>");

            File.WriteAllLines(file, contents);
        }


        public void LoadFrom_X3_Universe_Settings(string path)
        {
            GalaxyCreationSettings = X3Utils.LoadSettingsFile(path);
            if (GalaxyCreationSettings == null)
            {
                // If loading an old galaxy map, we will get here.  We just accept that we can't load old settings that don't exist.
            }
        }

        /// <summary>
        /// Unfortunately, because of the screwey nature of the nodes for a system (they represent totally different objects depending on type), we can't easily use an Xml deserializer,
        /// so we need to do this the hard way.
        /// </summary>
        /// <returns></returns>
        public void LoadFrom_X3_Universe_XML(string path)
        {
            XmlDocument doc = new XmlDocument();

            // project default is an x3_universe example file, generated by original Mayhem 3 galaxy generator.  197 sectors.
            doc.Load(path);
            Sectors.Clear();

            foreach (XmlNode sectornode in doc.DocumentElement.ChildNodes)
            {
                Sectors.Add(new X3Sector(sectornode));
            }

            MaxX = Sectors.Max(item => item.x) + 1;
            MaxY = Sectors.Max(item => item.y) + 1;
        }

        public void Load00044_XML(string path)
        {
            int streamID = 0;
            // Here, we're interested in loading the bottom 2 sections containing stream 1 and stream 2 sound pointers/lengths for the systems.
            // Find sector by matching the ID.
            // These are loaded into the matching sector.
            // 1.  Must set Sector's StreamID to 1 or 2.
            // 2.  Must set Sector's Stream Location and Stream Length

            List<string> content = File.ReadAllLines(path).ToList();

            foreach (string line in content)
            {
                string trimmedline = line.TrimStart();
                trimmedline = trimmedline.TrimStart('\t');

                if (trimmedline.StartsWith("<page id=\"7\" stream=\"1\">"))
                {
                    streamID = 1;
                    continue;
                }

                if (trimmedline.StartsWith("<page id=\"7\" stream=\"2\">"))
                {
                    streamID = 2;
                    continue;
                }

                if (streamID == 0) continue;
                if (trimmedline.StartsWith("</")) continue;
                if (trimmedline == "") continue;

                string id = X3Utils.GetXMLAttribute(trimmedline, "id");
                string s = X3Utils.GetXMLAttribute(trimmedline, "s");
                string l = X3Utils.GetXMLAttribute(trimmedline, "l");

                int xloc = int.Parse(id.Substring(5, 2));       // 1 - based locations.
                int yloc = int.Parse(id.Substring(3, 2));
                // Find the sector at this location and set it's Id and Name from the data we just retrieved.
                X3Sector sector = Sectors.Find(item => item.x == xloc - 1 && item.y == yloc - 1);
                if (sector == null)
                    throw new Exception($"Error reading 00044 file. System located at {xloc}:{yloc} not found in the loaded map.");

                sector.StreamID = streamID;
                sector.StreamLocation = int.Parse(s);
                sector.StreamLength = int.Parse(l);
            }
        }

        /// <summary>
        /// Contains sector options chosen by the user, and generated System stats such as Max Population.
        /// </summary>
        /// <param name="path"></param>
        public void Load9970_XML(string path)
        {
            // This beast is going to be far easier to parse manually than with an xml doc..  
            List<string> content = File.ReadAllLines(path).ToList();
            int section = 0;

            foreach (string line in content)
            {
                string trimmedline = line.TrimStart();
                trimmedline = trimmedline.TrimStart('\t');

                if (trimmedline.StartsWith("<page id=\"7\"") || trimmedline.StartsWith("<page id=\"19\"") || trimmedline.StartsWith("<page id=\"9970\""))
                {
                    section++;
                    continue;
                }
                if (trimmedline.StartsWith("</")) continue;
                if (trimmedline == "") continue;
                if (section == 0) continue;
                else if (section == 1)
                {
                    // Note:  This section has locations in format YYXX, which is normal. 
                    // <t id="1020101">Lasting Vengeance</t>   List of system ID' and names.  These names should match those we found in the MasterSystemList list which is what we actually use.
                    // For this section, we're just going to look up the Id in the sector list and make sure the name matches.
                    string id = X3Utils.GetXMLAttribute(trimmedline, "id");
                    string sectorname = X3Utils.GetXMLValue(trimmedline);

                    int xloc = int.Parse(id.Substring(5, 2));       // 1 - based locations. ...YYXX format.
                    int yloc = int.Parse(id.Substring(3, 2));
                    // Find the sector at this location and set it's Id and Name from the data we just retrieved.
                    X3Sector sector = Sectors.Find(item => item.x == xloc - 1 && item.y == yloc - 1);
                    if (sector == null)
                        throw new Exception($"Error reading 9970 file. System located at {xloc}:{yloc} not found in the loaded map.");
                    sector.SectorID = id;                   // format 102YYXX
                    sector.SectorName = sectorname;
                }
                else if (section == 2)
                {
                    // Note:  This section has locations in format YYXX, which is normal. 

                    // <t id="1030101">Lasting Vengeance \(x:0, y:0\)\n\nDejure owner: \033YTeladi\nCompany: \033YSalecrest Corp.\nAbnormal signals: \033Y48 %\nTerran memory: \033Ano\n\nMax. population: \033W107\nStation support: 9\nResearch rate: \033W99 %\n\nOutpost production: Vegetables\nOutpost consumption: Fruit\n</t>
                    // This is just DISPLAY data.  We are not going to verify it.  It is generated from the actual Sector Data in section 3.
                }
                else if (section == 3)
                {
                    // Note:  This section has locations in format XXYY, which is back-asswards.  Since it's his custom section, I guess he figured he'd be different.

                    string id = X3Utils.GetXMLAttribute(trimmedline, "id");
                    string value = X3Utils.GetXMLValue(trimmedline);

                    switch (id)
                    {
                        case "1":       // Galaxy Age
                            if (!Enum.TryParse(value, out GalaxyCreationSettings.GalaxyExpansion))
                                throw new Exception($"Invalid value found in 9970 file for galaxy age: {value}");
                            continue;
                        case "4":        // Universe Name
                            GalaxyCreationSettings.GalaxyName = value;
                            continue;
                    }
                    int intId = int.Parse(id);
                    int yIndex = (intId - 5) % 6;
                    int intValue = -1;

                    int.TryParse(value, out intValue);

                    if (intId > 4 && intId < 11)        // Argon relation.  Set GalaxyCreationSettings.Relations[Race][Race];
                    {
                        GalaxyCreationSettings.Relations[(int)ERace.Argon - 1, yIndex] = intValue;
                    }
                    if (intId > 10 && intId < 17)
                    {
                        GalaxyCreationSettings.Relations[(int)ERace.Boron - 1, yIndex] = intValue;
                    }
                    if (intId > 16 && intId < 23)
                    {
                        GalaxyCreationSettings.Relations[(int)ERace.Split - 1, yIndex] = intValue;
                    }
                    if (intId > 22 && intId < 29) 
                    {
                        GalaxyCreationSettings.Relations[(int)ERace.Paranid - 1, yIndex] = intValue;
                    }
                    if (intId > 28 && intId < 35)
                    {
                        GalaxyCreationSettings.Relations[(int)ERace.Teladi - 1, yIndex] = intValue;
                    }
                    if (intId > 34 && intId < 41)
                    {
                        GalaxyCreationSettings.Relations[5, yIndex] = intValue;
                    }
                    if (intId == 120)       // Maelstrom portal location, 0-based in format "X,Y"
                    {
                        string[] split = value.Split(',');
                        int x = int.Parse(split[0]);
                        int y = int.Parse(split[1]);
                        X3Sector set = Sectors.Find(item => item.x == x && item.y == y);
                        if (set != null)
                        {
                            set.IsMaelstromPortal = true;
                        }

                    }

                    // Now we get into actual Sector Stat Values that are used in the game.
                    // <t id="10060101">107</t>
                    if (intId > 10000000)       
                    {
                        int yloc = int.Parse(id.Substring(6, 2));   // Last 4       1-based system location in format XXYY
                        int xloc = int.Parse(id.Substring(4, 2));
                        string prefix = id.Remove(4);               // First 4      Determines which type of setting this is.  See switch below.

                        // Find the sector with the coordinate found here.
                        X3Sector sector = Sectors.Find(item => item.x == xloc - 1 && item.y == yloc - 1);
                        if (sector == null)
                            throw new Exception($"Unknown sector {id} found in section 3 of 9970 file.");

                        switch(prefix)
                        {
                            case "1000":            // Dejure Owner
                                sector.SectorCreationData.DejureOwner = intValue;
                                break;
                            case "1001":            // Smugglers (not used)
                                break;
                            case "1002":            // Xenons (abnormal signals)
                                sector.SectorCreationData.Xenons = intValue;
                                break;
                            case "1003":            // Terran memory
                                sector.SectorCreationData.Terrans = intValue;
                                break;
                            case "1004":            // Research
                                sector.SectorCreationData.Research = intValue;
                                break;
                            case "1005":            // Station Support
                                sector.SectorCreationData.Support = intValue;
                                break;
                            case "1006":            // Manpower (system population)
                                sector.SectorCreationData.Manpower = intValue;
                                break;
                            case "1007":            // Cluster (not used)
                                break;
                            case "1008":            // Production
                                sector.SectorCreationData.Production = intValue;
                                break;
                            case "1009":            // Consumption
                                sector.SectorCreationData.Consumption = intValue;
                                break;
                            case "1010":            // Company
                                sector.SectorCreationData.Company = intValue;
                                break;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Finds one sector ready to do something with.  
        /// Returns NULL if none exist.
        /// </summary>
        /// <returns></returns>
        public X3Sector GetRandomUninitializedSector(EQuadrant quadrant = EQuadrant.None)
        {
            // ensure we have at least one unitialized sector left.
            List<X3Sector> uninit;
            if (quadrant == EQuadrant.None)
            {
                uninit = Sectors.FindAll(item => item.IsInitialized == false);
            }
            else
            {
                uninit = Sectors.FindAll(item => item.IsInitialized == false && IsInQuadrant(item, quadrant));
            }

            if (uninit.Count == 0) return null;
            int randindex = m_Rand.Next(uninit.Count);
            return uninit.ElementAt(randindex);
        }

        public X3Sector GetCornerUninitializedSector()
        {
            List<X3Sector> uninit = Sectors.FindAll(item => item.IsInitialized == false);
            int y;

            List<X3Sector> leftside = uninit.FindAll(item => item.x == uninit.Min(t => t.x));
            List<X3Sector> rightside = uninit.FindAll(item => item.x == uninit.Max(t => t.x));
            List<X3Sector> side = m_Rand.Next(0, 2) == 0 ? leftside : rightside;

            if (m_Rand.Next(0, 2) == 0)
            {
                y = side.Min(item => item.y);
            }
            else
            {
                y = side.Max(item => item.y);
            }
            return side.Find(item => item.y == y);
        }

        /// <summary>
        /// Determines if a sector's location is within a particular quadrant of the universe.  Used when generating map positions for races.
        /// </summary>
        /// <param name="sec"></param>
        /// <param name="quadrant"></param>
        /// <returns></returns>
        public bool IsInQuadrant(X3Sector sec, EQuadrant quadrant)
        {
            switch(quadrant)
            {
                case EQuadrant.None:
                    return true;
                case EQuadrant.MiddleLeft:
                    return (sec.x > MaxX * 0.25) && (sec.x < MaxX * 0.50) && (sec.y > MaxY * 0.25) && (sec.y < MaxY * 0.50);
                case EQuadrant.MiddleRight:
                    return (sec.x > MaxX * 0.50) && (sec.x < MaxX * 0.75) && (sec.y > MaxY * 0.50) && (sec.y < MaxY * 0.75);
                case EQuadrant.UpperLeft:
                    return (sec.x < MaxX / 2) && (sec.y < MaxY / 2);
                case EQuadrant.UpperRight:
                    return (sec.x >= MaxX / 2) && (sec.y < MaxY / 2);
                case EQuadrant.LowerLeft:
                    return (sec.x < MaxX / 2) && (sec.y >= MaxY / 2);
                case EQuadrant.LowerRight:
                    return (sec.x >= MaxX / 2) && (sec.y >= MaxY / 2);
                default:
                    return true;
            }
        }


        public X3Sector GetSectorAt(int x, int y)
        {
            return Sectors.Find(item => item.x == x && item.y == y);
        }

        public List<X3Sector> GetSectorsBetween(X3Sector source, X3Sector dest)
        {
            if (source.x == dest.x)
            {
                return Sectors.FindAll(sector => sector.y.Between(source.y, dest.y));
            }
            else if (source.y == dest.y)
            {
                return Sectors.FindAll(sector => sector.x.Between(source.x, dest.x));
            }
            else
            {
                throw new Exception("GetSectorsBetween() does not work for diagnonal sectors");
            }
        }

        public bool HasNonVoidSectorsBetween(X3Sector source, X3Sector dest)
        {
            if (source.x == dest.x)
            {
                return Sectors.Exists(sector => sector.y.Between(source.y, dest.y) && !sector.IsVoid && sector.x == source.x);
            }
            else if (source.y == dest.y)
            {
                return Sectors.Exists(sector => sector.x.Between(source.x, dest.x) && !sector.IsVoid && sector.y == source.y);
            }
            else
            {
                throw new Exception("HasNonVoidSectorsBetween() does not work for diagnonal sectors");
            }
        }

        /// <summary>
        /// Returns list of all (1-4) neighboring sectors.
        /// </summary>
        /// <param name="sector"></param>
        /// <returns></returns>
        public List<X3Sector> GetNeighbors(X3Sector sector, int distance = 1)
        {
            List<X3Sector> nb = new List<X3Sector>();


            var west = Sectors.Find(item => item.x == sector.x - distance && item.y == sector.y);
            if (west != null) nb.Add(west);

            var east = Sectors.Find(item => item.x == sector.x + distance && item.y == sector.y);
            if (east != null) nb.Add(east);

            var north = Sectors.Find(item => item.x == sector.x && item.y == sector.y - distance);
            if (north != null) nb.Add(north);

            var south = Sectors.Find(item => item.x == sector.x && item.y == sector.y + distance);
            if (south != null) nb.Add(south);

            return nb;
        }

        /// <summary>
        /// Determines the sector that exists in a given direction.  Null if none exists.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public X3Sector GetSectorInDirection(X3Sector source, EGateDirection direction)
        {
            if (direction == EGateDirection.East || direction == EGateDirection.West)
            {
                for (int x = 1; x < MaxX; x++)
                {
                    int offset = x * ((direction == EGateDirection.East) ? 1 : -1);
                    X3Sector found = Sectors.Find(item => item.x == source.x + offset && item.y == source.y);
                    if (found != null) return found;
                }
            }
            else
            {
                for (int y = 1; y < MaxY; y++)
                {
                    int offset = y * ((direction == EGateDirection.South) ? 1 : -1);
                    X3Sector found = Sectors.Find(item => item.y == source.y + offset && item.x == source.x);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public List<X3Sector> GetConnectedNeighbors(X3Sector sector)
        {
            List<X3Sector> connected = new List<X3Sector>();
            foreach (X3Gate gate in sector.Gates)
            {
                X3Sector dest = Sectors.Find(item => item.x == gate.gx && item.y == gate.gy);
                if (dest == null) throw new Exception("GetConnectedNeigbors not able to find connected gate.");
                connected.Add(dest);
            }

            return connected;
        }


        /// <summary>
        /// Finds nearest neighbor (vertical or horizontal directions only) randomly up to a distance of 6.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public X3Sector GetNearestRandomNeighbor(X3Sector source, int startdistance = 1)
        {
            for (int x = startdistance; x < 7; x++)
            {
                List<X3Sector> neighbors = GetNeighbors(source, x);
                neighbors.RemoveAll(item => item.IsConnectedTo(source));

                if (neighbors.Count > 0)
                {
                    return neighbors[m_Rand.Next(neighbors.Count)];
                }
            }
            return null;
        }

        public void ClearAllGates()
        {
            foreach (X3Sector sector in Sectors)
            {
                sector.RemoveGates();
            }
        }

    }

   



    /// <summary>
    /// Type = 2
    /// <o t="2" s="15"/>
    /// 
    /// \mayhem_data\custom_backgrounds.txt contains indicies 0-81
    /// \mayhem_data\template_TBackgrounds.txt contains actual data for 81 backgrounds.
    /// 
    /// </summary>
    public class X3Background
    {
        public int s;     // represents sub-type which is the 0-index into types/TBackgrounds.pck

        public X3Background()
        {

        }

        public X3Background(XmlNode node)
        {
            s = int.Parse(node.Attributes["s"]?.InnerText);
        }

        public void CreateXml(List<string> contents)
        {
            contents.Add($"\t<o t=\"2\" s=\"{s}\"/>");
        }
    }

    /// <summary>
    /// Type = 3
    /// <o t="3" s="208" x="171233183" y="420674633" z="-167238936" color="16777160"/>
    /// </summary>
    public class X3Sun
    {
        private Random m_Rand = new Random();
        public const int t = 3;
        public int s;     // type of sun.index into type\TSuns.pck	0-218 index.See 11.cat.
        public int x;
        public int y;
        public int z;
        public int color;    // rgb color index. see appendix 3 in  http://www.xenotaph.net/lib_pdf/galaxy_hacking_x3tc.pdf


        public X3Sun(string rawxml)
        {
            rawxml = rawxml.Replace(" =", "=").Replace("= ", "=");
            // Here's a strange thing.  Sun ID's embedded in 11.cat\TSuns.pck look like this "SS_SUN_180" through "SS_SUN_189" for a given sun type.
            // What's interesting is those that end in a 4 are 50% sun level in game. Those that end in 5 are 100% in game. etc.. up through those that end with 8 are 300% sun level in game.
            // So we have to do the same here with our Sun ID because the template he provides has all the ID's and we don't want to be seeing sun levels lower than 100% in mayhem.
            // He used ID's 13X through 19X, where X is between 4 and 8 inclusive.
            s = int.Parse(X3Utils.GetXMLAttribute(rawxml, "s"));
            x = int.Parse(X3Utils.GetXMLAttribute(rawxml, "x"));
            y = int.Parse(X3Utils.GetXMLAttribute(rawxml, "y"));
            z = int.Parse(X3Utils.GetXMLAttribute(rawxml, "z"));
            color = int.Parse(X3Utils.GetXMLAttribute(rawxml, "color"));

            // Sun ID's only exist in these ranges in TSuns.
            if (s < 134)
                s = 134;
            if (s > 218)
                s = 218;
            // Test..  suns should be ID XX4 through XX8 which would be 100% to 300% sun levels.
            int value = X3Utils.PlaceDigitValue(0, s);      // Grab last digit of the sun ID.
            if ( value < 4 || value > 8)                    // If out of range of 50-300% sun level, put it back in range.
            {
                s = s - value + m_Rand.Next(4, 9);
            }
        }

        public void SetSunLevel(int val)
        {
            StringBuilder sb = new StringBuilder(s.ToString());
            sb[2] = val.ToString()[0];
            s = int.Parse(sb.ToString());
        }

        public X3Sun(XmlNode node)
        {
            s = int.Parse(node.Attributes["s"]?.InnerText);
            x = int.Parse(node.Attributes["x"]?.InnerText);
            y = int.Parse(node.Attributes["y"]?.InnerText);
            z = int.Parse(node.Attributes["z"]?.InnerText);
            color = int.Parse(node.Attributes["color"]?.InnerText);
        }

        public void CreateXml(List<string> contents)
        {
            contents.Add($"\t<o t=\"3\" s=\"{s}\" x=\"{x}\" y=\"{y}\" z=\"{z}\" color=\"{color}\"/>");
        }
    }

    /// <summary>
    /// Type = 4
    /// <o t="4" s="228" x="257613751" y="-31905822" z="-256982517"/>
    /// </summary>
    public class X3Planet
    {
        public const int t = 4;
        public int s;
        public int x;
        public int y;
        public int z;

        public X3Planet(string rawxml)
        {
            rawxml = rawxml.Replace(" =", "=").Replace("= ", "=");
            s = int.Parse(X3Utils.GetXMLAttribute(rawxml, "s"));
            x = int.Parse(X3Utils.GetXMLAttribute(rawxml, "x"));
            y = int.Parse(X3Utils.GetXMLAttribute(rawxml, "y"));
            z = int.Parse(X3Utils.GetXMLAttribute(rawxml, "z"));
        }

        public X3Planet(XmlNode node)
        {
            s = int.Parse(node.Attributes["s"]?.InnerText);
            x = int.Parse(node.Attributes["x"]?.InnerText);
            y = int.Parse(node.Attributes["y"]?.InnerText);
            z = int.Parse(node.Attributes["z"]?.InnerText);
        }

        public void CreateXml(List<string> contents)
        {
            contents.Add($"\t<o t=\"4\" s=\"{s}\" x=\"{x}\" y=\"{y}\" z=\"{z}\"/>");
        }
    }

    /// <summary>
    /// Type = 17
    /// <o t="17" s="7" x="-7480000" y="4027500" z="-15130500" atype="1" aamount="17" a="42338" b="10784" g="27456"/>
    /// </summary>
    public class X3Asteroid
    {
        public const int t = 17;
        public int s;           // sub-type, which is index into types\TAsteroids.pc     0-9.  See 07.cat.
        public int x;
        public int y;
        public int z;
        public int atype;       // atype - 0-Ore, 1=Silicon, 2=Nividium, 3=Ice
        public int aamount;     // aamount = yield quantity
        public int a;
        public int b;
        public int g;           // rotational factors.

        public X3Asteroid(XmlNode node)
        {
            ParseXml(node);
        }

        public X3Asteroid(string rawxml)
        {
            rawxml = rawxml.Replace(" =", "=").Replace("= ", "=");
            s = int.Parse(X3Utils.GetXMLAttribute(rawxml, "s"));
            x = int.Parse(X3Utils.GetXMLAttribute(rawxml, "x"));
            y = int.Parse(X3Utils.GetXMLAttribute(rawxml, "y"));
            z = int.Parse(X3Utils.GetXMLAttribute(rawxml, "z"));
            atype = int.Parse(X3Utils.GetXMLAttribute(rawxml, "atype"));
            aamount = int.Parse(X3Utils.GetXMLAttribute(rawxml, "aamount"));
            a = int.Parse(X3Utils.GetXMLAttribute(rawxml, "a"));
            b = int.Parse(X3Utils.GetXMLAttribute(rawxml, "b"));
            g = int.Parse(X3Utils.GetXMLAttribute(rawxml, "g"));
        }


        public void ParseXml(XmlNode node)
        {
            s = int.Parse(node.Attributes["s"]?.InnerText);
            x = int.Parse(node.Attributes["x"]?.InnerText);
            y = int.Parse(node.Attributes["y"]?.InnerText);
            z = int.Parse(node.Attributes["z"]?.InnerText);
            atype = int.Parse(node.Attributes["atype"]?.InnerText);
            aamount = int.Parse(node.Attributes["aamount"]?.InnerText);
            a = int.Parse(node.Attributes["a"]?.InnerText);
            b = int.Parse(node.Attributes["b"]?.InnerText);
            g = int.Parse(node.Attributes["g"]?.InnerText);
        }

        public void CreateXml(List<string> contents)
        {
            contents.Add($"\t<o t=\"17\" s=\"{s}\" x=\"{x}\" y=\"{y}\" z=\"{z}\" atype=\"{atype}\" aamount=\"{aamount}\" a=\"{a}\" b=\"{b}\" g=\"{g}\"/>");
        }
    }


    /// <summary>
    /// Type = 18
    /// <o t="18" s="3" x="22426343" y="7498788" z="-518692" gid="3" gx="1" gy="0" gtid="2" a="49152" b="0" g="0"/>
    /// Note about Location:
    ///     x = left to right, y = depth (what I would consider z-axis), z = top to bottom.
    ///     +z = up.  -z = down.
    ///     +x = right
    ///     x or z vary from ? to Sector size.  So for a system that is 25,000,000 size, the west gate may have x=-25,000,000 and an east gate having x=25,000,000
    ///     other gate position values seem to range between 1 and 8 mil, putting them slightly off axis.. so a west gate may sit slightly above or below the horizontal axis.
    ///     
    ///     ON-AXIS Alignment of gates is within 20% of system size (i.e. for 22Mil system size, gate variance was -3.9M to +3.9M)
    ///     OFF-AXIS Alighment of gate varied from 55% to 125% of system size.
    /// 
    /// </summary>
    public class X3Gate
    {
        public const int t = 18;
        public int s;           // SubType:  0 = North, 1 = South, 2 = West, 3 = East
        public int x;           // physical coordinates.   left-right 
        public int y;           // depth
        public int z;           // up-down
        public int gid;         // gid - always same as SubType.  denotes gate direction.
        public int gx;          // represents the DESTINATION system coordinates for the gate, 0 based
        public int gy;          // ""
        public int gtid;        // represents the DESTINATION gate direction.  So for an SubType=1 (south gate), the destination gate would be 0 (north)

        public bool IsRemoved = false;      // temporary to see stuff..
        public bool IsSpecial = false;

        // Gate rotations.
        // North Gate - a="0"     b="0" g="0"
        // South Gate - a="32768" b="0" g="0"
        // West Gate -  a="16384" b="0" g="0"
        // East Gate -  a="49152" b="0" g="0"
        public int a;
        public int b;
        public int g;    

        public X3Gate()
        {

        }
        
        public X3Gate(XmlNode node)
        {
            s = int.Parse(node.Attributes["s"]?.InnerText);
            x = int.Parse(node.Attributes["x"]?.InnerText);
            y = int.Parse(node.Attributes["y"]?.InnerText);
            z = int.Parse(node.Attributes["z"]?.InnerText);
            gid = int.Parse(node.Attributes["gid"]?.InnerText);
            gx = int.Parse(node.Attributes["gx"]?.InnerText);
            gy = int.Parse(node.Attributes["gy"]?.InnerText);
            gtid = int.Parse(node.Attributes["gtid"]?.InnerText);
            a = int.Parse(node.Attributes["a"]?.InnerText);
            b = int.Parse(node.Attributes["b"]?.InnerText);
            g = int.Parse(node.Attributes["g"]?.InnerText);
        }

        /// <summary>
        /// Serialize the gate and save the line to contents.
        /// </summary>
        /// <param name="contents"></param>
        public void CreateXml(List<string> contents)
        {
            contents.Add($"\t<o t=\"18\" s=\"{s}\" x=\"{x}\" y=\"{y}\" z=\"{z}\" gid=\"{gid}\" gx=\"{gx}\" gy=\"{gy}\" gtid=\"{gtid}\" a=\"{a}\" b=\"{b}\" g=\"{g}\" />");
        }
    }

    /// <summary>
    /// Type = 20
    /// <o t="20" s="SS_SPECIAL_SIGN3" x="867612" y="-1200845" z="-4453430" a="0" b="0" g="0" v="-1"/>
    /// </summary>
    public class X3Special
    {
        public const int t = 20;
        public string s;           // sub-type indexed in TSpecials.pck(SS_SPECIAL_SIGN1 through SS_SPECIAL_SIGN4) Odd donut shaped signs.
        public int x;
        public int y;
        public int z;           // position
        public int a;
        public int b;
        public int g;           // rotational factors.
        public int v;           // video stream to be played on advertising buoys.	may want to play with this.

        public X3Special(XmlNode node)
        {
            s = node.Attributes["s"]?.InnerText;
            x = int.Parse(node.Attributes["x"]?.InnerText);
            y = int.Parse(node.Attributes["y"]?.InnerText);
            z = int.Parse(node.Attributes["z"]?.InnerText);

            a = int.Parse(node.Attributes["a"]?.InnerText);
            b = int.Parse(node.Attributes["b"]?.InnerText);
            g = int.Parse(node.Attributes["g"]?.InnerText);

            v = -1;
        }

        public void CreateXml(List<string> contents)
        {
            contents.Add($"\t<o t=\"20\" s=\"{s}\" x=\"{x}\" y=\"{y}\" z=\"{z}\" a=\"{a}\" b=\"{b}\" g=\"{g}\" v=\"{v}\"/>");
        }
    }
}
