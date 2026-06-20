/**
	Template Name 	 : Mophy
	Author			 : DexignZone
	Version			 : 1.0
	Author Portfolio : https://themeforest.net/user/dexignzone/portfolio
	
**/

/* JavaScript Document */
jQuery(window).on('load', function() {
    'use strict';
	
	// Recent Blog
	if(jQuery('.recent-blog1').length > 0){
		var swiper = new Swiper('.recent-blog1',{
			speed: 1500,
			parallax: true,
			spaceBetween: 30,
			slidesPerView: 3,
			loop:true,
			autoplay: {
			   delay: 3000,
			},
			navigation: {
				prevEl: '.swiper-button-prev',
				nextEl: '.swiper-button-next',
			},
			breakpoints: {
				1200: {
					slidesPerView: 3,
				},
				768: {
					slidesPerView: 2,
				},
				320: {
					slidesPerView: 1,
				},
			}
		});
	}
	
		// service-silder-swiper
	if(jQuery('.main-swiper').length > 0){
		var mainSwiper = new Swiper(".main-swiper", {
			effect: "fade",
			speed: 1000,
			loop:true,
			slidesPerView: 1,
			autoplay: {
				delay: 150000000,
			},
			pagination: {
				el: ".main-swiper1-pagination",
				clickable: true,
				renderBullet: function (index, className) {
					return '<span class="' + className + '">' +"0"+ (index + 1) + "</span>";
				},
			},
			navigation: {
	          	nextEl: ".main-btn-next",
				prevEl: ".main-btn-prev",
	        },
		});
	
	}
	
	
	// Clients Swiper
	if(jQuery('.clients-swiper').length > 0){
		var swiper5 = new Swiper('.clients-swiper', {
			speed: 1500,
			parallax: true,
			slidesPerView: 5,
			spaceBetween: 30,
			autoplay: {
			   delay: 3000,
			},
			loop:true,
			navigation: {
				nextEl: '.swiper-button-next5',
				prevEl: '.swiper-button-prev5',
			},
			breakpoints: {
				1191: {
					slidesPerView: 6,
				},
				992: {
					slidesPerView: 4,
				},
				768: {
					slidesPerView: 3,
				},
				575: {
					slidesPerView: 3,
				},
				320: {
					slidesPerView: 2,
				},
			}
		});
	}

	/*  Testimonials Carousel = */
	if(jQuery('.testimonials-swiper').length > 0){
		var swiper5 = new Swiper('.testimonials-swiper', {
			speed: 2000,
			parallax: true,
			slidesPerView: 2,
			spaceBetween: 30,
			autoplay: {
			   delay: 3000,
			},
			loop:true,
			navigation: {
				nextEl: '.swiper-button-next',
				prevEl: '.swiper-button-prev',
			},
			
			breakpoints: {
				1191: {
					slidesPerView: 2,
				},
				992: {
					slidesPerView: 2,
				},
				768: {
					slidesPerView: 1,
				},
				320: {
					slidesPerView: 1,
				},
			}
		});
	}
	
	
});
/* Document .ready END */