#import <MobileCoreServices/MobileCoreServices.h>
#import <PhotosUI/PhotosUI.h>
#import <Photos/Photos.h>

extern UIViewController* UnityGetGLViewController();

#pragma mark - PHPicker Video Handler (iOS 14+)

API_AVAILABLE(ios(14))
@interface UNativeVideoPickerDelegate : NSObject <PHPickerViewControllerDelegate>
@end

@implementation UNativeVideoPickerDelegate

static UNativeVideoPickerDelegate *videoPickerDelegate;
static int videoPickerState = 0; // 0 -> none, 1 -> showing, 2 -> finished

+ (void)pickVideoFromLibrary
{
	PHPickerConfiguration *config = [[PHPickerConfiguration alloc] init];
	config.selectionLimit = 1;
	config.filter = [PHPickerFilter videosFilter];
	// preferredAssetRepresentationMode ensures we get a compatible copy
	if (@available(iOS 14, *)) {
		config.preferredAssetRepresentationMode = PHPickerConfigurationAssetRepresentationModeCurrent;
	}

	PHPickerViewController *picker = [[PHPickerViewController alloc] initWithConfiguration:config];

	if (videoPickerDelegate == nil)
		videoPickerDelegate = [[UNativeVideoPickerDelegate alloc] init];

	picker.delegate = videoPickerDelegate;
	videoPickerState = 1;

	[UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
}

+ (int)isVideoPickerBusy
{
	return (videoPickerState == 1) ? 1 : 0;
}

- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results
{
	[picker dismissViewControllerAnimated:YES completion:nil];

	if (results.count == 0)
	{
		videoPickerState = 0;
		UnitySendMessage("FPResultCallbackiOS", "OnOperationCancelled", "");
		return;
	}

	PHPickerResult *result = results[0];
	NSItemProvider *provider = result.itemProvider;

	// Try to load as movie (video) file
	if ([provider hasItemConformingToTypeIdentifier:UTTypeMovie.identifier])
	{
		[provider loadFileRepresentationForTypeIdentifier:UTTypeMovie.identifier completionHandler:^(NSURL * _Nullable url, NSError * _Nullable error) {
			if (error != nil || url == nil)
			{
				NSLog(@"[NativeFilePicker] Failed to load video: %@", error.localizedDescription);
				videoPickerState = 2;
				dispatch_async(dispatch_get_main_queue(), ^{
					UnitySendMessage("FPResultCallbackiOS", "OnFilePicked", "");
				});
				return;
			}

			// Copy to a temporary location since the provided URL is temporary
			NSString *tempDir = NSTemporaryDirectory();
			NSString *fileName = [url lastPathComponent];
			NSString *destPath = [tempDir stringByAppendingPathComponent:fileName];

			// Remove existing file if any
			[[NSFileManager defaultManager] removeItemAtPath:destPath error:nil];

			NSError *copyError = nil;
			[[NSFileManager defaultManager] copyItemAtURL:url toURL:[NSURL fileURLWithPath:destPath] error:&copyError];

			videoPickerState = 2;

			if (copyError != nil)
			{
				NSLog(@"[NativeFilePicker] Failed to copy video: %@", copyError.localizedDescription);
				dispatch_async(dispatch_get_main_queue(), ^{
					UnitySendMessage("FPResultCallbackiOS", "OnFilePicked", "");
				});
			}
			else
			{
				const char *pathUTF8 = [destPath UTF8String];
				char *pathCopy = (char *)malloc(strlen(pathUTF8) + 1);
				strcpy(pathCopy, pathUTF8);

				dispatch_async(dispatch_get_main_queue(), ^{
					UnitySendMessage("FPResultCallbackiOS", "OnFilePicked", pathCopy);
					free(pathCopy);
				});
			}
		}];
	}
	else
	{
		videoPickerState = 2;
		UnitySendMessage("FPResultCallbackiOS", "OnOperationCancelled", "");
	}
}

@end

#pragma mark - Document Picker (Original)

@interface UNativeFilePicker:NSObject
+ (void)pickFiles:(BOOL)allowMultipleSelection withUTIs:(NSArray<NSString *> *)allowedUTIs;
+ (void)exportFiles:(NSArray<NSURL *> *)paths;
+ (int)canPickMultipleFiles;
+ (int)isFilePickerBusy;
+ (char *)convertExtensionToUTI:(NSString *)extension;
@end

@implementation UNativeFilePicker

static UIDocumentPickerViewController *filePicker;
static BOOL pickingMultipleFiles;
static int filePickerState = 0; // 0 -> none, 1 -> showing, 2 -> finished

+ (void)pickFiles:(BOOL)allowMultipleSelection withUTIs:(NSArray<NSString *> *)allowedUTIs
{
	filePicker = [[UIDocumentPickerViewController alloc] initWithDocumentTypes:allowedUTIs inMode:UIDocumentPickerModeImport];
	filePicker.delegate = (id) self;

	if( @available(iOS 11.0, *) )
		filePicker.allowsMultipleSelection = allowMultipleSelection;

	// Show file extensions if possible
	if( @available(iOS 13.0, *) )
		filePicker.shouldShowFileExtensions = YES;

	pickingMultipleFiles = allowMultipleSelection;
	filePickerState = 1;

	[UnityGetGLViewController() presentViewController:filePicker animated:NO completion:^{ filePickerState = 0; }];
}

+ (void)exportFiles:(NSArray<NSURL *> *)paths
{
	if( paths != nil && [paths count] > 0 )
	{
		if ([paths count] > 1 && [self canPickMultipleFiles] == 1)
			filePicker = [[UIDocumentPickerViewController alloc] initWithURLs:paths inMode:UIDocumentPickerModeExportToService];
		else
			filePicker = [[UIDocumentPickerViewController alloc] initWithURL:paths[0] inMode:UIDocumentPickerModeExportToService];

		filePicker.delegate = (id) self;

		// Show file extensions if possible
		if( @available(iOS 13.0, *) )
			filePicker.shouldShowFileExtensions = YES;

		filePickerState = 1;
		[UnityGetGLViewController() presentViewController:filePicker animated:NO completion:^{ filePickerState = 0; }];
	}
}

+ (int)canPickMultipleFiles
{
	if( @available(iOS 11.0, *) )
		return 1;

	return 0;
}

+ (int)isFilePickerBusy
{
	if( filePickerState == 2 )
		return 1;

	if( filePicker != nil )
	{
		if( filePickerState == 1 || [filePicker presentingViewController] == UnityGetGLViewController() )
			return 1;
		else
		{
			filePicker = nil;
			return 0;
		}
	}
	else
		return 0;
}

// Credit: https://lists.apple.com/archives/cocoa-dev/2012/Jan/msg00052.html
+ (char *)convertExtensionToUTI:(NSString *)extension
{
	CFStringRef fileUTI = UTTypeCreatePreferredIdentifierForTag( kUTTagClassFilenameExtension, (__bridge CFStringRef) extension, NULL );
	char *result = [self getCString:(__bridge NSString *) fileUTI];
	CFRelease( fileUTI );

	return result;
}

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
+ (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentAtURL:(NSURL *)url
{
	[self documentPickerCompleted:controller documents:@[url]];
}
#pragma clang diagnostic pop

+ (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
	[self documentPickerCompleted:controller documents:urls];
}

+ (void)documentPickerCompleted:(UIDocumentPickerViewController *)controller documents:(NSArray<NSURL *> *)urls
{
	filePicker = nil;
	filePickerState = 2;

	if( controller.documentPickerMode == UIDocumentPickerModeImport )
	{
		if( !pickingMultipleFiles || [urls count] <= 1 )
		{
			const char* filePath;
			if( [urls count] == 0 )
				filePath = "";
			else
				filePath = [self getCString:[urls[0] path]];

			if( pickingMultipleFiles )
				UnitySendMessage( "FPResultCallbackiOS", "OnMultipleFilesPicked", filePath );
			else
				UnitySendMessage( "FPResultCallbackiOS", "OnFilePicked", filePath );
		}
		else
		{
			NSMutableArray<NSString *> *filePaths = [NSMutableArray arrayWithCapacity:[urls count]];
			for( int i = 0; i < [urls count]; i++ )
				[filePaths addObject:[urls[i] path]];

			UnitySendMessage( "FPResultCallbackiOS", "OnMultipleFilesPicked", [self getCString:[filePaths componentsJoinedByString:@">"]] );
		}
	}
	else if( controller.documentPickerMode == UIDocumentPickerModeExportToService )
	{
		if( [urls count] > 0 )
			UnitySendMessage( "FPResultCallbackiOS", "OnFilesExported", "1" );
		else
			UnitySendMessage( "FPResultCallbackiOS", "OnFilesExported", "0" );
	}

	[controller dismissViewControllerAnimated:NO completion:nil];
}

+ (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
	filePicker = nil;
	UnitySendMessage( "FPResultCallbackiOS", "OnOperationCancelled", "" );

	[controller dismissViewControllerAnimated:NO completion:nil];
}

// Credit: https://stackoverflow.com/a/37052118/2373034
+ (char *)getCString:(NSString *)source
{
	if( source == nil )
		source = @"";

	const char *sourceUTF8 = [source UTF8String];
	char *result = (char*) malloc( strlen( sourceUTF8 ) + 1 );
	strcpy(result, sourceUTF8);

	return result;
}

@end

extern "C" void _NativeFilePicker_PickFile( const char* UTIs[], int UTIsCount )
{
	NSMutableArray<NSString *> *allowedUTIs = [NSMutableArray arrayWithCapacity:UTIsCount];
	for( int i = 0; i < UTIsCount; i++ )
		[allowedUTIs addObject:[NSString stringWithUTF8String:UTIs[i]]];

	[UNativeFilePicker pickFiles:NO withUTIs:allowedUTIs];
}

extern "C" void _NativeFilePicker_PickMultipleFiles( const char* UTIs[], int UTIsCount )
{
	NSMutableArray<NSString *> *allowedUTIs = [NSMutableArray arrayWithCapacity:UTIsCount];
	for( int i = 0; i < UTIsCount; i++ )
		[allowedUTIs addObject:[NSString stringWithUTF8String:UTIs[i]]];

	[UNativeFilePicker pickFiles:YES withUTIs:allowedUTIs];
}

extern "C" void _NativeFilePicker_ExportFiles( const char* files[], int filesCount )
{
	NSMutableArray<NSURL *> *paths = [NSMutableArray arrayWithCapacity:filesCount];
	for( int i = 0; i < filesCount; i++ )
	{
		NSString *filePath = [NSString stringWithUTF8String:files[i]];
		[paths addObject:[NSURL fileURLWithPath:filePath]];
	}

	[UNativeFilePicker exportFiles:paths];
}

extern "C" int _NativeFilePicker_CanPickMultipleFiles()
{
	return [UNativeFilePicker canPickMultipleFiles];
}

extern "C" int _NativeFilePicker_IsFilePickerBusy()
{
	if (@available(iOS 14, *))
	{
		if ([UNativeVideoPickerDelegate isVideoPickerBusy] == 1)
			return 1;
	}
	return [UNativeFilePicker isFilePickerBusy];
}

extern "C" char* _NativeFilePicker_ConvertExtensionToUTI( const char* extension )
{
	return [UNativeFilePicker convertExtensionToUTI:[NSString stringWithUTF8String:extension]];
}

// New: Pick video from Photo Library using PHPickerViewController
extern "C" void _NativeFilePicker_PickVideoFromLibrary()
{
	if (@available(iOS 14, *))
	{
		[UNativeVideoPickerDelegate pickVideoFromLibrary];
	}
	else
	{
		// Fallback to document picker for iOS < 14
		NSArray<NSString *> *videoUTIs = @[@"public.movie"];
		[UNativeFilePicker pickFiles:NO withUTIs:videoUTIs];
	}
}