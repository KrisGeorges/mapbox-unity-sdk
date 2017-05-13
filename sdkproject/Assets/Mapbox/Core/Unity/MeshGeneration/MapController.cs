using System.Collections.Generic;
namespace Mapbox.Unity.MeshGeneration
{
	using Mapbox.Unity.MeshGeneration.Factories;
	using Mapbox.Unity.Map;
	using Mapbox.Map;
	using UnityEngine;
	using Mapbox.Unity.Utilities;
	using Utils;
	using Mapbox.Unity.MeshGeneration.Data;

	// TODO: abstract this class to IMap + AbstractMap
	public class MapController : MonoBehaviour
	{
		[Geocode]
		[SerializeField]
		string _latitudeLongitudeString;

		[SerializeField]
		int _zoom;
		public int Zoom
		{
			get
			{
				return _zoom;
			}
		}

		[SerializeField]
		AbstractTileProvider _tileProvider;

		[SerializeField]
		Factory[] _factories;

		[SerializeField]
		float _unityTileSize = 100;

		MapboxAccess _fileSouce;

		Vector2d _latitudeLongitude;

		UnityTile _tileTemplate;

		// HACK: lookup to remove tiles.
		Dictionary<UnwrappedTileId, UnityTile> _activeTiles = new Dictionary<UnwrappedTileId, UnityTile>();

		RectD _referenceTileRect;
		public RectD ReferenceTileRect
		{
			get
			{
				return _referenceTileRect;
			}
		}

		float _worldScaleFactor;
		public float WorldScaleFactor
		{
			get
			{
				return _worldScaleFactor;
			}
		}

		UnwrappedTileId _refTile;
		public UnwrappedTileId RefTile
		{
			get
			{
				return _refTile;
			}
		}

		void Awake()
		{
			_tileProvider.OnTileAdded += TileProvider_OnTileAdded;
			_tileProvider.OnTileRemoved += TileProvider_OnTileRemoved;
			_fileSouce = MapboxAccess.Instance;
		}

		void OnDestroy()
		{
			if (_tileProvider != null)
			{
				_tileProvider.OnTileAdded -= TileProvider_OnTileAdded;
				_tileProvider.OnTileAdded -= TileProvider_OnTileRemoved;
			}
		}

		public void SetLatitudeLongitude(string latitudeLongitudeString)
		{
			_latitudeLongitudeString = latitudeLongitudeString;
		}

		void Start()
		{
			var latLonSplit = _latitudeLongitudeString.Split(',');
			_latitudeLongitude = new Vector2d(double.Parse(latLonSplit[0]), double.Parse(latLonSplit[1]));

			_refTile = TileCover.CoordinateToTileId(_latitudeLongitude, _zoom);
			_referenceTileRect = Conversions.TileBounds(_refTile, _zoom);
			_worldScaleFactor = (float)(_unityTileSize / _referenceTileRect.Size.x);
			transform.localScale = Vector3.one * _worldScaleFactor;

			foreach (var factory in _factories)
			{
				factory.Initialize(_fileSouce);
			}
			_tileProvider.Initialize(this);
		}

		void TileProvider_OnTileAdded(object sender, Map.TileStateChangedEventArgs e)
		{
			// TODO delay any creation. Let factories handle this gameobject instantiation and such?
			UnityTile tile;

			if (_tileTemplate)
			{
				// Instantiate a duplicate of the last tile created. 
				// This helps to hide tile loading visuals.
				tile = Instantiate<UnityTile>(_tileTemplate);
				tile.name = e.TileId.ToString();
			}
			else
			{
				tile = new GameObject(e.TileId.ToString()).AddComponent<UnityTile>();
				tile.Zoom = _zoom;
				tile.RelativeScale = Conversions.GetTileScaleInMeters(0, _zoom) / Conversions.GetTileScaleInMeters((float)_latitudeLongitude.x, _zoom);
			}

			tile.TileCoordinate = new Vector2(e.TileId.X, e.TileId.Y);
			tile.Rect = Conversions.TileBounds(tile.TileCoordinate, _zoom);
			tile.transform.SetParent(transform, false);
			tile.transform.localPosition = new Vector3((float)(tile.Rect.Center.x - ReferenceTileRect.Center.x), 0, (float)(tile.Rect.Center.y - ReferenceTileRect.Center.y));

			// TODO: fix how template is created?
			_tileTemplate = tile;

			_activeTiles.Add(e.TileId, tile);

			foreach (var factory in _factories)
			{
				factory.Register(tile);
			}
		}

		void TileProvider_OnTileRemoved(object sender, TileStateChangedEventArgs e)
		{
			var unityTile = _activeTiles[e.TileId];
			foreach (var factory in _factories)
			{
				// TODO: implement! See below.
				factory.Unregister(unityTile);
			}

			// TODO; destroy or recycle objects in factories, instead.
			// Alternatively, have the tile provider generate UnityTile objects, instead?
			_activeTiles.Remove(e.TileId);

			// TODO: recycle!
			Destroy(unityTile.gameObject);
		}
	}
}