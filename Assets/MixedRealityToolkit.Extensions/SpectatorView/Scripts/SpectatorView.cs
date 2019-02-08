﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

using Microsoft.MixedReality.Toolkit.Extensions.SpectatorView.Interfaces;
using Microsoft.MixedReality.Toolkit.Extensions.SpectatorView.Utilities;

namespace Microsoft.MixedReality.Toolkit.Extensions.SpectatorView
{
    public class SpectatorView : MonoBehaviour
    {
        public Matrix4x4 LocalOriginToSharedOrigin = Matrix4x4.identity;

        [SerializeField] GameObject _sceneRoot;
        [SerializeField] MonoBehaviour MatchMakingService;
        [SerializeField] MonoBehaviour PlayerService;
        [SerializeField] MonoBehaviour NetworkingService;
        [SerializeField] MonoBehaviour SpatialCoordinateService;
        [SerializeField] List<MonoBehaviour> PlayerStateObservers;
        IMatchMakingService _matchMakingService;
        IPlayerService _playerService;
        INetworkingService _networkingService;
        ISpatialCoordinateService _spatialCoordinateService;
        List<IPlayerStateObserver> _playerStateObservers;

        bool _validState = true;

        void OnValidate()
        {
            FieldHelper.ValidateType<IMatchMakingService>(MatchMakingService);
            FieldHelper.ValidateType<IPlayerService>(PlayerService);
            FieldHelper.ValidateType<INetworkingService>(NetworkingService);
            FieldHelper.ValidateType<ISpatialCoordinateService>(SpatialCoordinateService);

            foreach(var observer in PlayerStateObservers)
            {
                FieldHelper.ValidateType<IPlayerStateObserver>(observer);
            }
        }

        void Awake()
        {
            _matchMakingService = MatchMakingService as IMatchMakingService;
            _playerService = PlayerService as IPlayerService;
            _networkingService = NetworkingService as INetworkingService;
            _spatialCoordinateService = SpatialCoordinateService as ISpatialCoordinateService;

            if (_matchMakingService == null ||
                _playerService == null ||
                _networkingService == null ||
                _spatialCoordinateService == null)
            {
                Debug.LogError("Invalid spectator view configuration, needed service classes are null or don't implement the correct interfaces");
                _validState = false;
            }

            _playerStateObservers = new List<IPlayerStateObserver>();
            foreach (var monoBehaviour in PlayerStateObservers)
            {
                var observer = monoBehaviour as IPlayerStateObserver;
                if (observer != null)
                    _playerStateObservers.Add(observer);
            }
        }

        void Start()
        {
            if (_validState)
            {
                _networkingService.DataReceived += OnDataReceivedEvent;
                _playerService.PlayerConnected += OnPlayerConnected;
                _playerService.PlayerDisconnected += OnPlayerDisconnected;

                _spatialCoordinateService.SpatialCoordinateStateUpdated += OnSpatialCoordinateStateUpdated;
            }
        }

        private void OnDataReceivedEvent(string playerId, byte[] payload)
        {
            _spatialCoordinateService.Sync(playerId, payload);
        }

        private void OnPlayerConnected(string playerId)
        {
            Debug.Log("Observed new player: " + playerId);
            foreach (var observer in _playerStateObservers)
            {
                observer.PlayerConnected(playerId);
            }
        }

        private void OnPlayerDisconnected(string playerId)
        {
            Debug.Log("Player lost: " + playerId);
            foreach (var observer in _playerStateObservers)
            {
                observer.PlayerDisconnected(playerId);
            }
        }

        private void OnSpatialCoordinateStateUpdated(byte[] payload)
        {
            if (_matchMakingService.IsConnected())
            {
                if(!_networkingService.SendData(payload))
                {
                    Debug.LogError("Networking service failed to send data");
                }
            }
        }

        void Update()
        {
            if (_validState)
            {
                // Make sure that root transform works by setting a 90 degree rotation along the y axis
                _sceneRoot.transform.position = Vector3.zero;
                _sceneRoot.transform.rotation = Quaternion.Euler(0, 90, 0);

                if (!_matchMakingService.IsConnected())
                {
                    // Setup the connection
                    _matchMakingService.Connect();

                    if (!_matchMakingService.IsConnected())
                    {
                        // If no network connection exists, we won't attempt any additional shared anchor logic
                        return;
                    }
                }

                // Update the world origin
                Matrix4x4 localOriginToSharedOrigin;
                if (_spatialCoordinateService.TryGetLocalOriginToSharedOrigin(out localOriginToSharedOrigin))
                {
                    LocalOriginToSharedOrigin = localOriginToSharedOrigin;

                    _sceneRoot.transform.position = LocalOriginToSharedOrigin.GetColumn(3);
                    _sceneRoot.transform.rotation = Quaternion.LookRotation(LocalOriginToSharedOrigin.GetColumn(2), LocalOriginToSharedOrigin.GetColumn(1));
                    Debug.Log("Updated root transform: position:" + _sceneRoot.transform.position.ToString() + ", rotation: " + _sceneRoot.transform.rotation.ToString());
                }
            }
        }
    }
}