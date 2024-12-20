import cv2
import mediapipe as mp
import json
import socket

# Initialize MediaPipe Holistic
mp_holistic = mp.solutions.holistic
mp_drawing = mp.solutions.drawing_utils

holistic = mp_holistic.Holistic(min_detection_confidence=0.5, min_tracking_confidence=0.5)

# Set up UDP socket
udp_host = '127.0.0.1'  # Localhost (change if necessary)
udp_port = 12345  # Must match Unity's listener port
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# Initialize OpenCV for video capture
cap = cv2.VideoCapture(0)

def get_avg(landmarks):
    """Helper function to calculate average of landmark coordinates."""
    return {
        'x': sum(landmark['x'] for landmark in landmarks) / len(landmarks),
        'y': sum(landmark['y'] for landmark in landmarks) / len(landmarks),
        'z': sum(landmark['z'] for landmark in landmarks) / len(landmarks),
        'visibility': sum(landmark['v'] for landmark in landmarks) / len(landmarks)  # Corrected visibility
    }


def add_extra_points(landmark_list):
    """Calculate additional points like hips, spine, and head for better pose representation."""
    
    # Ensure we're using dictionaries, not Landmark objects
    left_shoulder = landmark_list[11]
    right_shoulder = landmark_list[12]
    left_hip = landmark_list[23]
    right_hip = landmark_list[24]
    nose = landmark_list[0]
    left_ear = landmark_list[7]
    right_ear = landmark_list[8]
    left_mouth = landmark_list[9]
    right_mouth = landmark_list[10]

    # Add derived points to the list using dictionary-based landmarks
    landmark_list.append(get_avg([left_hip, right_hip]))  # Hip
    landmark_list.append(get_avg([left_hip, right_hip, left_shoulder, right_shoulder]))  # Spine
    landmark_list.append(get_avg([left_mouth, right_mouth, left_shoulder, right_shoulder]))  # Neck
    landmark_list.append(get_avg([nose, left_ear, right_ear]))  # Head

def convert_coordinates(landmark):
    """Convert MediaPipe NormalizedLandmark to a serializable dictionary."""
    return {
        "x": round(landmark.x, 3),
        "y": round(landmark.y, 3),
        "z": round(landmark.z, 3),
        "v": round(landmark.visibility, 3)  # Visibility rounded to 3 decimal places
    }

def send_data(data):
    """Send data over UDP after converting to JSON."""
    data_bytes = json.dumps(data).encode()
    sock.sendto(data_bytes, (udp_host, udp_port))

def get_landmark_data(results, frame_width, frame_height):
    """Extract landmarks and align coordinates for Unity."""
    landmark_data = {
        "pose": []
    }
    pos1 = pos2 = ""

    # Extract pose landmarks
    if results.pose_landmarks:
        for landmark in results.pose_landmarks.landmark:
            landmark_data["pose"].append(convert_coordinates(landmark))

    # Extract left and right hand landmarks
    if results.right_hand_landmarks:
        for lm in results.right_hand_landmarks.landmark:
            pos1 += f'{lm.x},{lm.y},{lm.z},'
        pos1 += 'Left,'
        # for landmark in results.left_hand_landmarks.landmark:
        #     landmark_data["left_hand"].append(convert_coordinates(landmark))

    if results.left_hand_landmarks:
        for lm in results.left_hand_landmarks.landmark:
            pos2 += f'{lm.x},{lm.y},{lm.z},'
        pos2 += 'Right,'
        # for landmark in results.right_hand_landmarks.landmark:
        #     landmark_data["right_hand"].append(convert_coordinates(landmark))

    # Combine hand data into a single string
    hand_data1 = pos1 + pos2
    # print(hand_data1)

    return landmark_data, hand_data1

while cap.isOpened():
    ret, frame = cap.read()
    if not ret:
        break

    frame = cv2.flip(frame, 1)  # Flip for a mirror view
    frame_height, frame_width, _ = frame.shape

    # Convert the image to RGB for MediaPipe processing
    rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

    # Process with Holistic model
    results = holistic.process(rgb_frame)

    # Extract and align landmarks
    landmark_data, hand_data1 = get_landmark_data(results, frame_width, frame_height)
    if landmark_data["pose"]:
        add_extra_points(landmark_data["pose"])
    # Convert to JSON and send over UDP
    send_data(landmark_data)
    # Send hand data to another port
    serverAddressPort = ('127.0.0.1', 5054)
    sock.sendto(str.encode(hand_data1), serverAddressPort)

    # Optionally, draw landmarks on the frame for visualization
    if results.pose_landmarks:
        mp_drawing.draw_landmarks(frame, results.pose_landmarks, mp_holistic.POSE_CONNECTIONS)
    if results.left_hand_landmarks:
        mp_drawing.draw_landmarks(frame, results.left_hand_landmarks, mp_holistic.HAND_CONNECTIONS)
    if results.right_hand_landmarks:
        mp_drawing.draw_landmarks(frame, results.right_hand_landmarks, mp_holistic.HAND_CONNECTIONS)

    # Display the annotated frame
    cv2.imshow("Holistic Pose and Hand Estimation", frame)

    # Exit on pressing 'q'
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# Release resources
cap.release()
cv2.destroyAllWindows()
