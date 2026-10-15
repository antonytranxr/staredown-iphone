package com.antony.staredown.mobile;

import android.graphics.Bitmap;
import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.face.Face;
import com.google.mlkit.vision.face.FaceDetection;
import com.google.mlkit.vision.face.FaceDetector;
import com.google.mlkit.vision.face.FaceDetectorOptions;
import java.util.List;
import java.util.Locale;

/** One camera is owned by Unity; this bridge only analyzes its pixels locally. */
public final class StaredownFaceDetector {
    private static FaceDetector detector;
    private static boolean busy;
    private static int generation;
    private static long sequence;
    private static String latest = "";
    private StaredownFaceDetector() {}

    public static synchronized boolean start() {
        stop();
        try {
            detector = FaceDetection.getClient(new FaceDetectorOptions.Builder()
                .setPerformanceMode(FaceDetectorOptions.PERFORMANCE_MODE_FAST)
                .setClassificationMode(FaceDetectorOptions.CLASSIFICATION_MODE_ALL)
                .setLandmarkMode(FaceDetectorOptions.LANDMARK_MODE_ALL)
                .setContourMode(FaceDetectorOptions.CONTOUR_MODE_NONE)
                .setMinFaceSize(.18f).enableTracking().build());
            return true;
        } catch (Exception error) { latest = ""; return false; }
    }
    public static synchronized void stop() {
        generation++; busy = false; latest = "";
        if (detector != null) { detector.close(); detector = null; }
    }
    public static synchronized boolean readyForFrame() { return detector != null && !busy; }
    public static synchronized String poll() { return latest; }

    // ARGB rows start at the image top. Rotation is clockwise and unmirrored.
    public static synchronized void analyze(int[] argb, int width, int height, int rotation, double timestamp) {
        if (detector == null || busy || argb == null || width < 100 || height < 100 ||
            width > 1920 || height > 1920 || argb.length != width * height) return;
        busy = true;
        final int token = generation;
        final Bitmap bitmap;
        try { bitmap = Bitmap.createBitmap(argb, width, height, Bitmap.Config.ARGB_8888); }
        catch (Exception error) { busy = false; return; }
        try {
            detector.process(InputImage.fromBitmap(bitmap, ((rotation % 360) + 360) % 360))
                .addOnSuccessListener(faces -> publish(faces, timestamp, token))
                .addOnFailureListener(error -> publish(null, timestamp, token))
                .addOnCompleteListener(task -> {
                    bitmap.recycle();
                    synchronized (StaredownFaceDetector.class) { if (generation == token) busy = false; }
                });
        } catch (Exception error) { bitmap.recycle(); busy = false; publish(null, timestamp, token); }
    }
    private static synchronized void publish(List<Face> faces, double timestamp, int token) {
        if (token != generation) return;
        boolean valid = faces != null && faces.size() == 1;
        float left = 0, right = 0, yaw = 0, pitch = 0;
        if (valid) {
            Face face = faces.get(0);
            Float l = face.getLeftEyeOpenProbability(), r = face.getRightEyeOpenProbability();
            yaw = face.getHeadEulerAngleY(); pitch = face.getHeadEulerAngleX();
            valid = l != null && r != null && Math.abs(yaw) <= 18 && Math.abs(pitch) <= 25 &&
                face.getBoundingBox().width() >= 100 && face.getBoundingBox().height() >= 100;
            if (valid) { left = 1 - l; right = 1 - r; }
        }
        latest = String.format(Locale.US,
            "{\"sequence\":%d,\"timestamp\":%.6f,\"valid\":%s,\"left\":%.5f,\"right\":%.5f,\"yaw\":%.3f,\"pitch\":%.3f}",
            ++sequence, timestamp, valid ? "true" : "false", left, right, yaw, pitch);
    }
}
