export async function onRequestGet(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    try {
        const token = request.headers.get("Authorization");

        console.log('Spray GET request received with token:', token);
        console.log('Must match token:', env.APITOKEN);

        // Verify authorization token against environment variable
        if (!token || token !== env.APITOKEN) {
            return new Response("Unauthorized", { status: 401 });
        }

        // Extract user ID from URL parameters
        const url = new URL(request.url);
        const userId = url.searchParams.get('userid');

        if (!userId) {
            return new Response("User ID is required", { status: 400 });
        }

        // Extract the actual ID after the @ symbol (e.g., 1234@steam -> 1234)
        const actualId = userId.includes('@') ? userId.split('@')[0] : userId;

        if (!actualId) {
            return new Response("Invalid user ID format", { status: 400 });
        }

        console.log('Looking for spray data for user ID:', userId, 'extracted ID:', actualId);

        // Construct KV key using the extracted ID
        const kvKey = `spray_${actualId}`;

        // Look up spray data in KV storage
        const sprayData = await env.KV.get(kvKey, "json") as {
            pixelString?: string;
            pixelFrames?: string[];
            isGif?: boolean;
        } | null;

        if (!sprayData) {
            console.log('No spray data found for user ID:', userId, 'extracted ID:', actualId);
            return new Response("Spray data not found", { status: 404 });
        }

        // Check if this is a GIF and handle accordingly
        if (sprayData.isGif === true) {
            // For GIFs, return the pixelFrames array
            const pixelFrames = sprayData.pixelFrames;

            if (!pixelFrames || !Array.isArray(pixelFrames) || pixelFrames.length === 0) {
                console.log('No pixelFrames found in spray data for user ID:', userId, 'extracted ID:', actualId);
                return new Response("Pixel frames not found", { status: 404 });
            }

            console.log('Successfully retrieved pixelFrames for user ID:', userId, 'extracted ID:', actualId, 'frame count:', pixelFrames.length);
            return new Response(JSON.stringify(pixelFrames), {
                status: 200,
                headers: {
                    'Content-Type': 'application/json'
                }
            });
        } else {
            // For static images, return the pixelString
            const pixelString = sprayData.pixelString;

            if (!pixelString) {
                console.log('No pixelString found in spray data for user ID:', userId, 'extracted ID:', actualId);
                return new Response("Pixel string not found", { status: 404 });
            }

            console.log('Successfully retrieved pixelString for user ID:', userId, 'extracted ID:', actualId);
            return new Response(pixelString, {
                status: 200,
                headers: {
                    'Content-Type': 'text/plain'
                }
            });
        }

    } catch (error) {
        console.error('Spray GET error:', error);
        return new Response("Server error", { status: 500 });
    }
}