using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using System.Diagnostics;
using FivePD.API.Utils;
using LocationData;

LocationData.AddLocation("PowerStationGetAwayDriver", new Vector3(2729.98f,1704.97f,224.23f));

namespace SirGerbain_PrisonerEscape
{
    [CalloutProperties("Prisoner Escape", "sirGerbain", "1.0.0")]
    public class SirGerbain_PrisonerEscape : FivePD.API.Callout
    {
        Vector3[] startLocations =
        {
            new Vector3(2051.47f,-887.94f,79.14f), //288.45 - Sustancia Road// 
            LocationData.Locations.Location["SustanciaRoad"]
        };

        Vector3[] dropOffLocations =
        {
            // new Vector3(2730.65f,1382.87f,24.12f), //291.75 - power station,
            LocationData.Locations.Location["PowerStation"]
        };

        Vector3[] getawayDriverStartLocations =
        {
            // new Vector3(2729.98f,1704.97f,224.23f), //89.67 - power station,
            LocationData.Locations.Location["PowerStationGetAwayDriver"]
        };

        Vector3 startLocation, absoluteStartLocation;
        Ped prisonGuard, getawayDriver; //PrisGuard01SMM
        List<Ped> prisoners = new List<Ped>();
        List<PedHash> prisonerHashList = new List<PedHash>();
        List<PedHash> getawayDriverHashList = new List<PedHash>();
        List<VehicleHash> transportVehicles = new List<VehicleHash>();
        Vehicle prisonTransport, getawayCar;
        Random random = new Random();
        int startLocationIndex, transportVehicleIndex, getAwayDriverIndex;
        bool transportActive = true;
        bool followRouteActive = false;
        bool followEscapeRoute = false;
        bool initiateGetaway = false;

        public SirGerbain_PrisonerEscape()
        {
            startLocationIndex = random.Next(0, startLocations.Length - 1);
            startLocation = startLocations[startLocationIndex];

            float offsetX = random.Next(100, 200);
            float offsetY = random.Next(100, 200);
            Vector3 playerPos = Game.PlayerPed.Position;
            startLocation = new Vector3(offsetX, offsetY, 0) + playerPos;
            absoluteStartLocation= World.GetNextPositionOnStreet(startLocation);

            InitInfo(absoluteStartLocation);

            prisonerHashList.Add(PedHash.Prisoner01);
            prisonerHashList.Add(PedHash.Prisoner01SMY);
            prisonerHashList.Add(PedHash.PrisMuscl01SMY);

            transportVehicles.Add(VehicleHash.PBus);
            transportVehicleIndex = random.Next(0, transportVehicles.Count);

            getawayDriverHashList.Add(PedHash.Chimp);
            getawayDriverHashList.Add(PedHash.MexGoon03GMY);
            getawayDriverHashList.Add(PedHash.SalvaGoon01GMY);
            getawayDriverHashList.Add(PedHash.ChiGoon01GMM);
            getAwayDriverIndex = random.Next(0, getawayDriverHashList.Count);

            InitInfo(startLocation);
            ShortName = "Prisoner Transport";
            CalloutDescription = "Guard is held at gun point";
            ResponseCode = 3;
            StartDistance = 300f;
        }
        public async override Task OnAccept()
        {
            InitBlip(300, BlipColor.Red);
            UpdateData();
            
            prisonGuard = await SpawnPed(PedHash.Prisguard01SMM, startLocation);
            prisonGuard.AlwaysKeepTask = true;
            prisonGuard.BlockPermanentEvents = true;

            /*for (int i = 0; i < 3; i++)
            {
                float offsetX = 2.0f * (float)Math.Cos(i * 120.0f * (Math.PI / 180.0));
                float offsetY = 2.0f * (float)Math.Sin(i * 120.0f * (Math.PI / 180.0));
                Vector3 prisonerLocation = startLocation + new Vector3(offsetX, offsetY, 0);
                Ped prisoner = await SpawnPed(prisonerHashList[random.Next(0, prisonerHashList.Count)], prisonerLocation);
                    prisoner.AlwaysKeepTask = true;
                    prisoner.BlockPermanentEvents = true;
                prisoners.Add(prisoner);
            } */

            await createPrisonersAtLocation(startLocation, 3);

            prisonTransport = await SpawnVehicle(transportVehicles[transportVehicleIndex], absoluteStartLocation);
            prisonTransport.Heading = 288.45f;
            prisonGuard.SetIntoVehicle(prisonTransport, VehicleSeat.Driver);
            prisoners[0].SetIntoVehicle(prisonTransport, VehicleSeat.RightRear);
            prisoners[0].Weapons.Give(WeaponHash.Pistol, 250, true, true);
            prisoners[1].SetIntoVehicle(prisonTransport, VehicleSeat.LeftRear);
            prisoners[2].SetIntoVehicle(prisonTransport, VehicleSeat.ExtraSeat1);
            prisonGuard.Task.CruiseWithVehicle(prisonTransport, 40f, 524828);

            Notify("A prison transport lost GPS signal in the area.");
            DrawSubtitle("Code 6 the area and report when you find the transport. Use caution!", 7000);

        }

        public async override void OnStart(Ped closest)
        {
            base.OnStart(closest);

            while (transportActive && !followRouteActive)
            {
                await BaseScript.Delay(1000);
                float distance = prisonTransport.Position.DistanceToSquared(absoluteStartLocation);
                if (distance > 350f) {
                    Notify("Air-1 has spotted the transport.");

                    Vehicle helicopter = await SpawnVehicle(VehicleHash.Polmav, new Vector3(prisonTransport.Position.X, prisonTransport.Position.Y, 60));
                        helicopter.CurrentRPM = 5000;
                    Ped pilot = await SpawnPed(PedHash.Prisguard01SMM, new Vector3(prisonTransport.Position.X, prisonTransport.Position.Y, 50));
                        pilot.SetIntoVehicle(helicopter, VehicleSeat.Driver);
                        pilot.Task.ChaseWithHelicopter(prisonTransport, new Vector3(35f,35f,35f));

                    prisonTransport.AttachBlip().ShowRoute = true;
                    prisonGuard.Task.CruiseWithVehicle(prisonTransport, 100f, 524828);
                    transportActive = false;
                    break;
                }
                
            }

            while (!followRouteActive)
            {
                await BaseScript.Delay(1000);
                float distance = prisonTransport.Position.DistanceToSquared(Game.PlayerPed.Position);
                if (distance < 1000f)
                {
                    Notify("Prisoner transport located. We'll enable tracking.");
                    prisonTransport.AttachBlip();
                    //prisonGuard.Task.CruiseWithVehicle(prisonTransport, 100f, 524828);
                    prisonGuard.Task.DriveTo(prisonTransport, dropOffLocations[0], 0, 150f, 786988);

                    followRouteActive = true;
                    break;
                }
            }

            while (!followEscapeRoute)
            {
                await BaseScript.Delay(1000);
                float distance = prisonTransport.Position.DistanceToSquared(dropOffLocations[0]);
                if (distance < 200f)
                {

                    using (TaskSequence guardSequence = new TaskSequence())
                    {
                        guardSequence.AddTask.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                        guardSequence.AddTask.HandsUp(-1);
                        guardSequence.Close();
                        prisonGuard.Task.PerformSequence(guardSequence);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        prisoners[i].Task.ClearAll();
                        using (TaskSequence prisonerSequence = new TaskSequence())
                        {
                            prisonerSequence.AddTask.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                            //prisonerSequence.AddTask.GoTo(prisonGuard.Position+new Vector3(1f,1f,0f));
                            if (i == 0)
                            {
                                prisonerSequence.AddTask.AimAt(prisonGuard, -1);
                            }
                            prisonerSequence.Close();
                            prisoners[i].Task.PerformSequence(prisonerSequence);
                        }
                    }

                    getawayDriver = await SpawnPed(getawayDriverHashList[getAwayDriverIndex], getawayDriverStartLocations[0]);
                    getawayDriver.AlwaysKeepTask = true;
                    getawayDriver.BlockPermanentEvents = true;

                    getawayCar = await SpawnVehicle(VehicleHash.Schafter3, getawayDriverStartLocations[0], 89.67f);
                    getawayCar.Mods.PrimaryColor = VehicleColor.MetallicBlack;
                    getawayCar.Mods.SecondaryColor = VehicleColor.MetallicBlack;
                    getawayCar.Mods.PearlescentColor = VehicleColor.MetallicBlack;
                    getawayCar.LockStatus = VehicleLockStatus.Unlocked;
                    getawayCar.Heading = 89.67f;

                    getawayDriver.SetIntoVehicle(getawayCar, VehicleSeat.Driver);
                    getawayDriver.Task.DriveTo(getawayCar, new Vector3(2748.52f, 1385.12f, 24.08f), 0, 100f, 262700);

                    followEscapeRoute = true;
                    break;
                }
            }

            while (!initiateGetaway)
            {
                await BaseScript.Delay(1000);
                float distance = getawayDriver.Position.DistanceToSquared(new Vector3(2748.52f, 1385.12f, 24.08f));
                if (distance < 20f)
                {
                    getawayDriver.Task.ClearAll();
                    initiateGetaway = true;
                    break;
                }
            }

        }
        public async Task createPrisonersAtLocation(Vector3 location, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                float offsetX = 2.0f * (float)Math.Cos(i * 120.0f * (Math.PI / 180.0));
                float offsetY = 2.0f * (float)Math.Sin(i * 120.0f * (Math.PI / 180.0));
                Vector3 prisonerLocation = location + new Vector3(offsetX, offsetY, 0);
                Ped prisoner = await SpawnPed(prisonerHashList[random.Next(0, prisonerHashList.Count)], prisonerLocation);
                    prisoner.AlwaysKeepTask = true;
                    prisoner.BlockPermanentEvents = true;
                prisoners.Add(prisoner);
            }
        }
        public override void OnCancelBefore()
        {
            base.OnCancelBefore();
            //prisonTransport.Delete();
        }
        public void Notify(string message)
        {
            ShowNetworkedNotification(message, "CHAR_CALL911", "CHAR_CALL911", "Dispatch", "Prisoner Transport", 15f);
        }
        private void DrawSubtitle(string message, int duration)
        {
            API.BeginTextCommandPrint("STRING");
            API.AddTextComponentSubstringPlayerName(message);
            API.EndTextCommandPrint(duration, false);
        }
    }
}
