using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace VOTE
{
    public class InteractableVOTE : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        //public bool nullGetsAVote = true;
        public float maxAge = 0.2f;

        [Serializable]
        public struct SampledInteractable
        {
            public float timeStamp;
            [CanBeNull] public Interactable votedInteractable;
        }

        public List<SampledInteractable> sampledInteractables = new List<SampledInteractable>();

        public string nameOfCurrentVote;

        // Update is called once per frame
        void Update()
        {
            sampledInteractables.RemoveAll(s => Time.time - s.timeStamp > maxAge);
            var i = EvaluateVote();
            if (i != null)
            {
                nameOfCurrentVote = i.name;
            }
        }

        [ItemCanBeNull] private Dictionary<Interactable, int> votes = new Dictionary<Interactable?, int>();
        [CanBeNull]
        public Interactable EvaluateVote()
        {
            sampledInteractables.RemoveAll(s => Time.time - s.timeStamp > maxAge);
            
            votes.Clear();
            var leaderVotes = 0;
            Interactable leaderInteractable = null;
            foreach (var s in sampledInteractables)
            {
                if (!votes.ContainsKey(s.votedInteractable)) votes.Add(s.votedInteractable, 0);
                votes[s.votedInteractable] += 1;
                if (votes[s.votedInteractable] <= leaderVotes) continue;
                leaderInteractable = s.votedInteractable;
                leaderVotes = votes[s.votedInteractable];
            }
            return leaderInteractable;

        }

        public void VoteForInteractable(Interactable i)
        {
            if (i!=null)
                sampledInteractables.Add(new SampledInteractable { timeStamp = Time.time, votedInteractable = i });
        }
    }

}