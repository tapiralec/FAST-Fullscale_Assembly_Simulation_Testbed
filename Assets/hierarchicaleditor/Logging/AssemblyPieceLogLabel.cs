using System;
using System.Collections;
using System.Collections.Generic;
using PlayStructure;
using UnityEngine;

public class AssemblyPieceLogLabel : XRTLogging.PointedAtLabel
{
    //make it so we only need to drop this component onto the piece.
    private void Reset()
    {
        if (TryGetComponent(out BuildingPiece buildingPiece))
        {
            _label =
                $"{buildingPiece.pieceType.ToString()}_{buildingPiece.pieceColor.ToString()}_{buildingPiece.buildingPieceID}";
        }
        else if (TryGetComponent(out ScrewPiece screwPiece))
        {
            _label = $"SCREW_{screwPiece.screwIndex}";
        }
        else if (TryGetComponent(out Screwdriver screwdriver))
        {
            _label = $"SCREWDRIVER";
        }
    }
}
