using System;
using System.Collections;
using System.Collections.Generic;
using PlayStructure;
using System.Linq;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace PlayStructure
{
    public class ScrewPiece : MonoBehaviour
    {

        public int screwIndex = -1;
        
        [SerializeField]
        private AttachPoint currentAP;

        public void InsertIfValidScrewHoleNearby()
        {
            var ap = CheckIfHoleToAttachTo();
            // Make sure there's a valid ap nearby:
            if (ap==null) return;
            // Check if this is good for the current step first:
            if (!PlayInstructions.instance.CheckScrewInsert(ap)) return;
            TryAttach(ap);
        }
        
        public AttachPoint CheckIfHoleToAttachTo(float distanceThresh = 0.05f)
        {
            return BuildStructure.instance.GetClosestAttachPoint(transform.position, out var ap,
                AttachPoint.AttachState.LOOSE_LOCK, pipesOnly:true) ? ap : null;
            //if (Vector3.Magnitude(closestAP.attachTransform.position - transform.position) < distanceThresh &&
                    //closestAP.owningPiece.isPipe) //make sure it's a pipe and not something else.
                //{
                    //return closestAP;
                //}
            //}
        }
        

        public Vector3 insertPositionOffset => new Vector3(0.002f, 0f, 0f);
        public Quaternion insertRotationOffset => Quaternion.Euler(90f, 0f, 180f);

        public bool suspendInteractionCuesOnScrewInAnimation = true;
        public bool resumeInteractionCuesOnScrewInAnimationComplete = true;

        public void TryAttach(AttachPoint ap)
        {
            if (ap.currentAttachState == AttachPoint.AttachState.LOOSE_LOCK)
            {
                // set the rigidbody to be ready to be under something:
                var rigidbody = GetComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                
                // move it in the hierarchy to where it belongs:
                transform.parent = ap.attachTransform;
                transform.localPosition = new Vector3(0.002f, 0f, 0f);
                transform.localRotation = Quaternion.Euler(90f, 0f, 180f);

                currentAP = ap;
                ap.DoPartialLock(this);
            }
        }

        // This might be able to be made nicer with direct drive stuff? but it's kinda weird since
        // you need to interface with it via a tool, rather than directly with the hand. So for now,
        // just doing with code.
        public void TryScrewIn(Screwdriver screwdriver)
        {
            if (!PlayInstructions.instance.CheckKeyUsage(currentAP)) return;
            if (currentAP != null &&
                currentAP.currentAttachState == AttachPoint.AttachState.PARTIAL_LOCK)
            {

                
                // drop the throwable and rigidbody entirely:
                var thisThrowable = GetComponent<Valve.VR.InteractionSystem.Throwable>();
                if (thisThrowable != null)
                {
                    Destroy(thisThrowable);
                }

                var thisRigidbody = GetComponent<Rigidbody>();
                if (thisRigidbody != null)
                {
                    Destroy(thisRigidbody);
                }

                var thisInteractable = GetComponent<Valve.VR.InteractionSystem.Interactable>();
                if (thisInteractable != null)
                {
                    Destroy(thisInteractable);
                }

                //Launch the animation:
                animatedKey = Instantiate(animatedKeyPrefab);
                heldScrewdriver = screwdriver;
                Debug.Log("Ready to begin screw-in animation");
                StartCoroutine(AnimateScrewIn());
                //transform.localPosition = Vector3.zero;
                //transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                
                currentAP.DoFullLock();
            }
        }

        [SerializeField] private GameObject animatedKeyPrefab;
        private GameObject animatedKey;
        private Screwdriver heldScrewdriver;
        private int animPhase = 0;
        [SerializeField] private float[] animationSpeeds = { 0.2f, 0.2f, 0.2f };
        private float currentTimer = 0f;
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalPosition;
        private Quaternion keyRotationInHand;
        private Vector3 keyPositionInHand;
        IEnumerator AnimateScrewIn()
        {
            while (animPhase < 5)
            {
                currentTimer += Time.deltaTime;
                var flip180 = Quaternion.Angle(heldScrewdriver.transform.rotation,
                    transform.parent.rotation * Quaternion.Euler(90f, 0f, 180f) * Quaternion.Euler(0f, 90f, 0f)) > 90f
                    ? Quaternion.Euler(0f, 0f, 180f)
                    : Quaternion.identity;
                switch (animPhase)
                {
                    case 0:
                        // Hide the held one and set up the animation only screwdriver into place.
                        if (animatedKey == null)
                        {
                            //Debug.Log("animated key still doesn't exist right");
                            break;
                        }
                        // Lock real key here:
                        if (Player.instance.leftHand.currentAttachedObject == heldScrewdriver.gameObject)
                        {
                            Player.instance.leftHand.isLocked = true;
                        }
                        if (Player.instance.rightHand.currentAttachedObject == heldScrewdriver.gameObject)
                        {
                            Player.instance.rightHand.isLocked = true;
                        }
                        // start suspending hint animations while turning:
                        if (suspendInteractionCuesOnScrewInAnimation && PlayInstructions.instance != null && PlayInstructions.instance.interactionCue != null)
                            PlayInstructions.instance.interactionCue.suspendHintAnimations = true;

                        animatedKey.transform.position = heldScrewdriver.transform.position;
                        animatedKey.transform.rotation = heldScrewdriver.transform.rotation;
                        heldScrewdriver.GetComponent<MeshRenderer>().enabled = false;
                        animPhase = 1;
                        currentTimer = 0f;
                        break;
                    case 1:
                        //Debug.Log("animating from held to screw");
                        // Move from the held screwdriver to where we need to start the turn
                        animatedKey.transform.position = Vector3.Lerp(heldScrewdriver.transform.position,
                            transform.parent.position +
                            transform.parent.rotation * new Vector3(0.002f, 0f, 0f) +
                            transform.parent.rotation * new Vector3(0.03f, 0f, 0f),
                            currentTimer / animationSpeeds[0]);
                        animatedKey.transform.rotation = Quaternion.Lerp(heldScrewdriver.transform.rotation,
                            transform.parent.rotation * Quaternion.Euler(90f, 0f, 180f) * Quaternion.Euler(0f, 90f, 0f) * flip180,
                            currentTimer / animationSpeeds[0]);
                        if (currentTimer > animationSpeeds[0])
                        {
                            //Debug.Log("anim key at screw now");
                            animPhase = 2;
                            currentTimer = 0f;
                            animatedKey.transform.parent = transform;
                            animatedKey.transform.position =
                                transform.parent.position +
                                transform.parent.rotation * new Vector3(0.002f, 0f, 0f) +
                                transform.parent.rotation * new Vector3(0.03f, 0f, 0f);
                            animatedKey.transform.rotation =
                                transform.parent.rotation * Quaternion.Euler(90f, 0f, 180f) * Quaternion.Euler(0f, 90f, 0f);
                            initialLocalPosition = transform.localPosition;
                            initialLocalRotation = transform.localRotation;
                        }

                        break;
                    case 2:
                        //Debug.Log("Animating screw turn");
                        // Turn the screwdriver and the screw
                        transform.localPosition = Vector3.Lerp(initialLocalPosition, Vector3.zero,
                            currentTimer / animationSpeeds[2]);
                        transform.localRotation = Quaternion.Lerp(initialLocalRotation,
                            Quaternion.Euler(0f, 0f, 180f), currentTimer / animationSpeeds[2]);
                        if (currentTimer > animationSpeeds[1])
                        {
                            //Debug.Log("Screw turned now");
                            animPhase = 3;
                            currentTimer = 0f;
                            transform.localPosition = Vector3.zero;
                            transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                            animatedKey.transform.parent = null;
                        }
                        break;
                    case 3:
                        // Return the held screwdriver
                        //Debug.Log("Animating key return");
                        animatedKey.transform.position = Vector3.Lerp(
                            transform.parent.position +
                            transform.parent.rotation * new Vector3(0.002f, 0f, 0f) +
                            transform.parent.rotation * new Vector3(0.03f, 0f, 0f),
                            heldScrewdriver.transform.position,
                            currentTimer / animationSpeeds[2]);
                        animatedKey.transform.rotation = Quaternion.Lerp(
                            transform.parent.rotation * Quaternion.Euler(90f, 0f, 180f) * Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f,0f,90f) * flip180,
                            heldScrewdriver.transform.rotation,
                            currentTimer / animationSpeeds[2]);
                        if (currentTimer > animationSpeeds[2])
                        {
                            //Debug.Log("Key back where it should be now");
                            animPhase = 4;
                            currentTimer = 0f;
                            animatedKey.transform.position = heldScrewdriver.transform.position;
                            animatedKey.transform.rotation = heldScrewdriver.transform.rotation;
                        }

                        break;
                    case 4:
                        // Unlock real key here:
                        if (Player.instance.leftHand.currentAttachedObject == heldScrewdriver.gameObject)
                        {
                            Player.instance.leftHand.isLocked = false;
                        }
                        if (Player.instance.rightHand.currentAttachedObject == heldScrewdriver.gameObject)
                        {
                            Player.instance.rightHand.isLocked = false;
                        }
                        // stop suspending hint animations since we're done turning:
                        if (resumeInteractionCuesOnScrewInAnimationComplete && PlayInstructions.instance != null && PlayInstructions.instance.interactionCue != null)
                            PlayInstructions.instance.interactionCue.suspendHintAnimations = false;
                        
                        //Debug.Log("Destroying animated key");
                        Destroy(animatedKey);
                        heldScrewdriver.GetComponent<MeshRenderer>().enabled = true;
                        // Reenable the held screwdriver and remove our temporary copy.
                        animPhase = 5;
                        break;
                    default:
                        throw new NotImplementedException("Invalid animation phase");
                }

                yield return new WaitForEndOfFrame();
            }
            animPhase = 0;

        }
        
        

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }

}