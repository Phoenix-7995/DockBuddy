package com.dockbuddy.app;

/**
 * The look and personality of one cat: fur, eyes, how often it blinks and wanders, and how it cheers.
 * Three cats are defined here: Ginny (cutest), Caspar (boss) and Mimi (friendly).
 */
final class CatStyle {
    /** How the cat reacts when a deck button succeeds. */
    static final int CHEER_SQUINT = 0; // happy closed eyes, big smile
    static final int CHEER_WINK = 1;   // winks and smirks
    static final int CHEER_SOFT = 2;   // soft closed eyes, head tilt

    final int fur, patch, inner, eye, muzzle, blush, defaultBg;
    final boolean hasPatch, hasMuzzle, hasBlush, smirk, sideGlance, tilt;
    final float eyeR, eyeDx, lid;           // fractions of the cat's size
    final long blinkMin, blinkSpan, wanderMin, wanderSpan;
    final int cheerMode;

    private CatStyle(Builder b) {
        fur = b.fur; patch = b.patch; inner = b.inner; eye = b.eye; muzzle = b.muzzle; blush = b.blush;
        defaultBg = b.defaultBg;
        hasPatch = b.patch != 0; hasMuzzle = b.muzzle != 0; hasBlush = b.blush != 0;
        smirk = b.smirk; sideGlance = b.sideGlance; tilt = b.tilt;
        eyeR = b.eyeR; eyeDx = b.eyeDx; lid = b.lid;
        blinkMin = b.blinkMin; blinkSpan = b.blinkSpan; wanderMin = b.wanderMin; wanderSpan = b.wanderSpan;
        cheerMode = b.cheerMode;
    }

    private static class Builder {
        int fur, patch, inner, eye, muzzle, blush, defaultBg;
        boolean smirk, sideGlance, tilt;
        float eyeR = 0.10f, eyeDx = 0.17f, lid;
        long blinkMin = 2500, blinkSpan = 3000, wanderMin = 1200, wanderSpan = 2000;
        int cheerMode = CHEER_SQUINT;
    }

    /** Ginny: orange, big shiny green eyes, rosy cheeks, blinks often and bounces her gaze around. */
    static final CatStyle GINNY = new CatStyle(new Builder() {{
        fur = 0xFFF0A04B; patch = 0; inner = 0xFFFFB3B3; eye = 0xFF9CCC65; muzzle = 0xFFFFF3E0;
        blush = 0x88FF8A80; defaultBg = 0xFF26A69A;
        eyeR = 0.12f; eyeDx = 0.18f;
        blinkMin = 1200; blinkSpan = 2200; wanderMin = 600; wanderSpan = 1400;
        cheerMode = CHEER_SQUINT;
    }});

    /** Caspar: white with a grey cap, sleepy heavy lids, amber eyes, slow glances and a smirk. */
    static final CatStyle CASPAR = new CatStyle(new Builder() {{
        fur = 0xFFF5F5F5; patch = 0xFF7A7A7A; inner = 0xFFE0A0A0; eye = 0xFFFFB300;
        defaultBg = 0xFF37474F;
        eyeR = 0.09f; eyeDx = 0.16f; lid = 0.42f;
        smirk = true; sideGlance = true;
        blinkMin = 3500; blinkSpan = 3000; wanderMin = 2200; wanderSpan = 2600;
        cheerMode = CHEER_WINK;
    }});

    /** Mimi: white with a soft grey patch, blue eyes, a gentle smile, and a head tilt when happy. */
    static final CatStyle MIMI = new CatStyle(new Builder() {{
        fur = 0xFFFAFAFA; patch = 0xFF8D8F9A; inner = 0xFFF2B8C6; eye = 0xFF4FC3F7; muzzle = 0xFFFFFFFF;
        defaultBg = 0xFF5C6BC0;
        eyeR = 0.11f; eyeDx = 0.17f;
        blinkMin = 2000; blinkSpan = 2500; wanderMin = 1200; wanderSpan = 2000;
        tilt = true;
        cheerMode = CHEER_SOFT;
    }});
}
