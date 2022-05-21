using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlayStructure
{
    public class BucketManager : Singleton<BucketManager>
    {

        [SerializeField]

    private List<PieceSpawnerBucket> _mBuckets;
        public List<PieceSpawnerBucket> buckets => _mBuckets ??= FindObjectsOfType<PieceSpawnerBucket>().ToList();
        public PieceSpawnerBucket GetBucket(PieceType pieceType, PieceColor pieceColor=PieceColor.BLACK)
        {
            var bucket = buckets.Find(b => b.pieceType == pieceType && b.pieceColor == pieceColor);
            if (bucket == null)
            {
                throw new Exception("Could not find a bucket with that piece type and color");
            }
            return bucket;
        }

    }

}