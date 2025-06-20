﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public class CinematicManager : IDisposable
    {
        // Remote location information
        Player player;

        public Milliseconds m_cinematicDiff;
        public ServerTime m_lastCinematicCheck;
        public CinematicSequencesRecord m_activeCinematic;
        public int m_activeCinematicCameraIndex;
        public Milliseconds m_cinematicLength;
        public IReadOnlyList<FlyByCamera> m_cinematicCamera;
        Position m_remoteSightPosition;
        TempSummon m_CinematicObject;

        public CinematicManager(Player playerref)
        {
            player = playerref;
            m_activeCinematicCameraIndex = -1;
            m_remoteSightPosition = new Position(0.0f, 0.0f, 0.0f);
        }

        public virtual void Dispose()
        {
            if (m_cinematicCamera != null && m_activeCinematic != null)
                EndCinematic();
        }

        public void BeginCinematic(CinematicSequencesRecord cinematic)
        {
            m_activeCinematic = cinematic;
            m_activeCinematicCameraIndex = -1;
        }
        
        public void NextCinematicCamera()
        {
            // Sanity check for active camera set
            if (m_activeCinematic == null || m_activeCinematicCameraIndex >= m_activeCinematic.Camera.Length)
                return;

            int cinematicCameraId = m_activeCinematic.Camera[++m_activeCinematicCameraIndex];
            if (cinematicCameraId == 0)
                return;

            var flyByCameras = M2Storage.GetFlyByCameras(cinematicCameraId);
            if (!flyByCameras.Empty())
            {
                // Initialize diff, and set camera
                m_cinematicDiff = Milliseconds.Zero;
                m_cinematicCamera = flyByCameras;

                if (!m_cinematicCamera.Empty())
                {
                    FlyByCamera firstCamera = m_cinematicCamera.FirstOrDefault();
                    Position pos = new(firstCamera.locations.X, firstCamera.locations.Y, firstCamera.locations.Z, firstCamera.locations.W);
                    if (!pos.IsPositionValid())
                        return;

                    player.GetMap().LoadGridForActiveObject(pos.GetPositionX(), pos.GetPositionY(), player);
                    m_CinematicObject = player.SummonCreature(1, pos.posX, pos.posY, pos.posZ, 0.0f, TempSummonType.TimedDespawn, (Minutes)5);
                    if (m_CinematicObject != null)
                    {
                        m_CinematicObject.SetActive(true);
                        player.SetViewpoint(m_CinematicObject, true);
                    }

                    // Get cinematic length
                    m_cinematicLength = m_cinematicCamera.LastOrDefault().timeStamp;
                }
            }
        }

        public void EndCinematic()
        {
            if (m_activeCinematic == null)
                return;

            m_cinematicDiff = Milliseconds.Zero;
            m_cinematicCamera = null;
            m_activeCinematic = null;
            m_activeCinematicCameraIndex = -1;
            if (m_CinematicObject != null)
            {
                WorldObject vpObject = player.GetViewpoint();
                if (vpObject != null)
                    if (vpObject == m_CinematicObject)
                        player.SetViewpoint(m_CinematicObject, false);

                m_CinematicObject.AddObjectToRemoveList();
            }
        }

        public void UpdateCinematicLocation(TimeSpan diff)
        {
            if (m_activeCinematic == null || m_activeCinematicCameraIndex == -1 || m_cinematicCamera == null || m_cinematicCamera.Count == 0)
                return;

            Position lastPosition = new();
            Milliseconds lastTimestamp = Milliseconds.Zero;
            Position nextPosition = new();
            Milliseconds nextTimestamp = Milliseconds.Zero;

            // Obtain direction of travel
            foreach (FlyByCamera cam in m_cinematicCamera)
            {
                if (cam.timeStamp > m_cinematicDiff)
                {
                    nextPosition = new Position(cam.locations.X, cam.locations.Y, cam.locations.Z, cam.locations.W);
                    nextTimestamp = cam.timeStamp;
                    break;
                }
                lastPosition = new Position(cam.locations.X, cam.locations.Y, cam.locations.Z, cam.locations.W);
                lastTimestamp = cam.timeStamp;
            }
            float angle = lastPosition.GetAbsoluteAngle(nextPosition);
            angle -= lastPosition.GetOrientation();
            if (angle < 0)
                angle += 2 * MathFunctions.PI;

            // Look for position around 2 second ahead of us.
            Milliseconds workDiff = m_cinematicDiff;

            // Modify result based on camera direction (Humans for example, have the camera point behind)
            workDiff += (Milliseconds)(2000 * Math.Cos(angle));

            // Get an iterator to the last entry in the cameras, to make sure we don't go beyond the end
            var endItr = m_cinematicCamera.LastOrDefault();
            if (endItr != null && workDiff > endItr.timeStamp)
                workDiff = endItr.timeStamp;

            // Never try to go back in time before the start of cinematic!
            if (workDiff < 0)
                workDiff = m_cinematicDiff;

            // Obtain the previous and next waypoint based on timestamp
            foreach (FlyByCamera cam in m_cinematicCamera)
            {
                if (cam.timeStamp >= workDiff)
                {
                    nextPosition = new Position(cam.locations.X, cam.locations.Y, cam.locations.Z, cam.locations.W);
                    nextTimestamp = cam.timeStamp;
                    break;
                }
                lastPosition = new Position(cam.locations.X, cam.locations.Y, cam.locations.Z, cam.locations.W);
                lastTimestamp = cam.timeStamp;
            }

            // Never try to go beyond the end of the cinematic
            if (workDiff > nextTimestamp)
                workDiff = nextTimestamp;

            // Interpolate the position for this moment in time (or the adjusted moment in time)
            Milliseconds timeDiff = nextTimestamp - lastTimestamp;
            Milliseconds interDiff = workDiff - lastTimestamp;
            float xDiff = nextPosition.posX - lastPosition.posX;
            float yDiff = nextPosition.posY - lastPosition.posY;
            float zDiff = nextPosition.posZ - lastPosition.posZ;
            Position interPosition = new(lastPosition.posX + (xDiff * ((float)interDiff / timeDiff)), lastPosition.posY +
                (yDiff * ((float)interDiff / timeDiff)), lastPosition.posZ + (zDiff * ((float)interDiff / timeDiff)));

            // Advance (at speed) to this position. The remote sight object is used
            // to send update information to player in cinematic
            if (m_CinematicObject != null && interPosition.IsPositionValid())
                m_CinematicObject.MonsterMoveWithSpeed(interPosition.posX, interPosition.posY, interPosition.posZ, (Speed)500.0f, false, true);

            // If we never received an end packet 10 seconds after the final timestamp then force an end
            if (m_cinematicDiff > m_cinematicLength + (Seconds)10)
                EndCinematic();
        }

        public bool IsOnCinematic() { return m_cinematicCamera != null; }
    }
}
