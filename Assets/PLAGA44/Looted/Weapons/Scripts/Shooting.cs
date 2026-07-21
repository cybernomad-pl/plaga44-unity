using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shooting : MonoBehaviour
{
   
    public float fireRate = 0.1f;
    public GameObject bulletPrefab;

    float elapsedTime;

    public Transform nozzleTransform;

 
    public Animator gunAnimator;


    public OVRInput.Button ShootingButton;

    public GameObject slicerGameobject;

    // Update is called once per frame
    void Update()
    {
        //elapsed time
        elapsedTime += Time.deltaTime;

        if (!Plaga44.UI.HamburgerMenu.MenuOpen && OVRInput.GetDown(ShootingButton, OVRInput.Controller.RTouch))
        {
            if (elapsedTime > fireRate)
            {
                Shoot();
                
                elapsedTime = 0;
            }
        }

    }

    private void Shoot()
    {
        //Play sound
        if (AudioManager.instance != null && AudioManager.instance.gunSound != null)
        {
            AudioManager.instance.gunSound.gameObject.transform.position = nozzleTransform.position;
            AudioManager.instance.gunSound.Play();
        }

        //Play animation
        if (gunAnimator != null)
            gunAnimator.SetTrigger("Fire");


        //Create the bullet
        if (bulletPrefab == null || nozzleTransform == null) return;
        GameObject bulletGameobject = Instantiate(bulletPrefab, nozzleTransform.position, Quaternion.Euler(0, 0, 0));
        bulletGameobject.transform.forward = nozzleTransform.forward;

        if (slicerGameobject != null && slicerGameobject.GetComponent<Collider>() != null)
            Physics.IgnoreCollision(bulletGameobject.GetComponent<Collider>(),slicerGameobject.GetComponent<Collider>());

    }

   


}
