System.config({
	baseURL: '/',
	map: {
		'jquery': 'js/lib/jquery/jquery.js',
		'tether': 'js/lib/tether/tether.js',
		'bootstrap': 'js/lib/bootstrap/bootstrap.js',
		'angular2': 'js/lib/angular2/angular2.dev.js'
	},
	meta: {
		'js/lib/bootstrap/bootstrap.js': {
			deps: [
				'jquery',
				'tether'
			]
		},
		'js/lib/tether/tether.js': {
			format: 'global',
			exports: 'Tether'
		}
	},
	bundles: {
		'sitecore': [
			'jquery',
			'tether',
			'bootstrap'
		]
	}
});
