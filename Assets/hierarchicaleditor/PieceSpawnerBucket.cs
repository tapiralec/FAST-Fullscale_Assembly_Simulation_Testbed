using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace PlayStructure
{
    public class PieceSpawnerBucket : MonoBehaviour
    {
        // The piece type and color that this bucket will handle spawning in.
        public PieceType pieceType;
        public PieceColor pieceColor;

        public GameObject prefabToSpawn;

        //Question: if the objects are spawned in as determined by the instruction step, on completion of the
        // prior step, then they need a place to spawn in at, hence spawnTransform.
        // if, however, the objects are spawned in as a result of reaching into a region and grabbing an object,
        // then it makes more sense for the objects to spawn at a good position and orientation relative to the hand
        // (and be grasped immediately). Ideally, each prefab should have its own definition for an "ideal" grasp.
        // ("ideal" grasp *won't* necessarily be used, just definitely used for unseen spawn-grabs like this)
        // The latter approach will thus be able to make use of whatever approach we use for the force-drop+force-regrab
        // that we'll have in place for the multi-part structures.
        // Also, check SpawnAndAttachToHand in SteamVR.
        public Transform spawnTransform;
        
        // Start is called before the first frame update
        void Start()
        {
            if (prefabToSpawn == null || prefabToSpawn.GetComponent<BuildingPiece>()==null)
            {
                throw new Exception($"Need to define a prefab to spawn in PieceSpawnerBucket {transform.name}, "+
                                    $"and it must have a BuildingPiece component.");
            }
        }

        // Update is called once per frame
        void Update()
        {

        }

        public BuildingPiece SpawnPiece(int pieceID)
        {
            //SpawnAndAttachToHand()
            var newObj = Instantiate(prefabToSpawn);
            var newPiece = newObj.GetComponent<BuildingPiece>();
            newPiece.buildingPieceID = pieceID;
            newPiece.transform.position = spawnTransform.position;
            newPiece.transform.rotation = spawnTransform.rotation;
            return newPiece;
        }
        
    }

}